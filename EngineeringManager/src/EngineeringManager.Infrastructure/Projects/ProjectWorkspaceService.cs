using System.Text.Json;
using EngineeringManager.Application.Finance;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Finance;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Projects;

public sealed class ProjectWorkspaceService(ApplicationDbContext db) : IProjectWorkspaceService
{
    public async Task<ProjectWorkspaceDto?> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await db.Projects
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.ResponsibleUser)
            .Include(item => item.Department)
            .Include(item => item.Branch)
            .Include(item => item.LegalEntities).ThenInclude(item => item.LegalEntity)
            .Include(item => item.TaxConfigurations)
            .Include(item => item.Contracts).ThenInclude(item => item.LineItems)
            .Include(item => item.Milestones)
            .Include(item => item.Assignments).ThenInclude(item => item.User)
            .Include(item => item.Partners).ThenInclude(item => item.Partner)
            .Include(item => item.Partners).ThenInclude(item => item.Contract)
            .SingleOrDefaultAsync(item => item.Id == projectId && item.IsActive, cancellationToken);
        if (project is null) return null;

        var settlements = await db.FinanceSettlements.AsNoTracking()
            .Include(item => item.Contract).Include(item => item.LegalEntity).Include(item => item.BusinessPartner)
            .Include(item => item.Adjustments).Include(item => item.Deductions)
            .Where(item => item.ProjectId == projectId)
            .OrderByDescending(item => item.BusinessDate)
            .ToListAsync(cancellationToken);
        var receivables = settlements.Where(item => item.Direction == LedgerDirection.Receivable)
            .Select(item => new ProjectReceivableItemDto(item.Id, item.BusinessDate, null, item.Contract?.ContractNumber,
                item.LegalEntity.ShortName, item.BusinessPartner?.Name, CurrentSettlementAmount(item), item.Notes, item.Status == LedgerRecordStatus.Voided,
                item.ContractId, item.LegalEntityId, item.BusinessPartnerId, item.ConcurrencyStamp))
            .ToList();
        var payables = settlements.Where(item => item.Direction == LedgerDirection.Payable)
            .Select(item => new ProjectPayableItemDto(item.Id, item.BusinessDate, null, item.Contract?.ContractNumber,
                item.LegalEntity.ShortName, item.BusinessPartner?.Name ?? "未填写合作单位", CurrentSettlementAmount(item), item.Notes, item.Status == LedgerRecordStatus.Voided,
                item.ContractId, item.LegalEntityId, item.BusinessPartnerId, item.ConcurrencyStamp))
            .ToList();

        var cashHeaders = await db.FinanceCashEntries.AsNoTracking()
            .Include(item => item.LegalEntity).Include(item => item.BusinessPartner).Include(item => item.Account)
            .Include(item => item.Allocations).ThenInclude(item => item.Settlement).ThenInclude(item => item.Contract)
            .Where(item => item.Allocations.Any(allocation => allocation.ProjectId == projectId) ||
                (item.SourceType == LedgerSourceType.ProjectCollection && item.SourceId == projectId))
            .OrderByDescending(item => item.BusinessDate)
            .ToListAsync(cancellationToken);
        var collections = cashHeaders.Where(item => item.Direction == LedgerDirection.Receivable && !item.IsReversal)
            .Select(item =>
            {
                var allocations = item.Allocations.Where(allocation => allocation.ProjectId == projectId).ToArray();
                var first = allocations.FirstOrDefault();
                return new ProjectCollectionItemDto(item.Id, item.BusinessDate, first?.Settlement.Contract?.ContractNumber,
                    item.LegalEntity.ShortName, item.BusinessPartner?.Name, item.Account?.AccountName ?? "未填写账户",
                    item.Amount, item.PaymentMethod, item.Notes,
                    allocations.Length == 1 ? first!.SettlementId : null, first?.ContractId, item.LegalEntityId, item.BusinessPartnerId, item.AccountId, item.ConcurrencyStamp);
            })
            .ToList();
        var payments = cashHeaders.Where(item => item.Direction == LedgerDirection.Payable && !item.IsReversal)
            .Select(item =>
            {
                var allocations = item.Allocations.Where(allocation => allocation.ProjectId == projectId).ToArray();
                var first = allocations[0];
                return new ProjectPaymentItemDto(item.Id, item.BusinessDate, first.Settlement.Contract?.ContractNumber,
                    item.LegalEntity.ShortName, item.BusinessPartner?.Name ?? "未填写合作单位", item.Account?.AccountName ?? "未填写账户",
                    allocations.Sum(allocation => allocation.Amount), ParsePaymentMethod(item.PaymentMethod), item.Notes,
                    allocations.Length == 1 ? first.SettlementId : null, first.ContractId, item.LegalEntityId, item.BusinessPartnerId, item.AccountId, item.ConcurrencyStamp);
            })
            .ToList();

        var invoiceHeaders = await db.FinanceInvoices.AsNoTracking()
            .Include(item => item.LegalEntity).Include(item => item.BusinessPartner)
            .Include(item => item.Allocations).ThenInclude(item => item.Settlement).ThenInclude(item => item.Contract)
            .Where(item => item.Direction == LedgerDirection.Receivable && item.Allocations.Any(allocation => allocation.ProjectId == projectId))
            .OrderByDescending(item => item.InvoiceDate)
            .ToListAsync(cancellationToken);
        var invoices = invoiceHeaders.Select(item =>
        {
            var allocations = item.Allocations.Where(allocation => allocation.ProjectId == projectId).ToArray();
            var first = allocations[0];
            var gross = allocations.Sum(allocation => allocation.Amount);
            var ratio = item.Amount == 0m ? 0m : gross / item.Amount;
            return new ProjectInvoiceItemDto(item.Id, item.InvoiceDate, item.InvoiceNumber,
                item.Direction == LedgerDirection.Receivable ? InvoiceDirection.Output : InvoiceDirection.Input,
                first.Settlement.Contract?.ContractNumber, item.LegalEntity.ShortName, item.BusinessPartner?.Name,
                item.TaxRate ?? 0m, (item.NetAmount ?? 0m) * ratio, (item.TaxAmount ?? 0m) * ratio, gross,
                item.Status == LedgerRecordStatus.Voided ? InvoiceStatus.Voided : InvoiceStatus.IssuedOrReceived,
                first.ContractId, item.LegalEntityId, item.BusinessPartnerId, item.InvoiceType, item.ProjectTaxConfigurationId, item.ConcurrencyStamp);
        }).ToList();
        var payrollCrewRows = await db.PayrollPayments.AsNoTracking()
            .Where(item => item.RecipientType == EngineeringManager.Domain.Employees.PayrollRecipientType.CrewWorker &&
                item.CrewBusinessPartnerId.HasValue && item.Batch.ProjectId == projectId &&
                item.Batch.Status != EngineeringManager.Domain.Employees.PayrollBatchStatus.Voided &&
                (item.Batch.PaymentDate.HasValue || item.PaymentDate.HasValue))
            .Select(item => new
            {
                item.Id,
                item.PayrollBatchId,
                PaymentDate = item.Batch.PaymentDate ?? item.PaymentDate!.Value,
                item.Batch.LegalEntityId,
                LegalEntityName = item.Batch.LegalEntity != null ? item.Batch.LegalEntity.ShortName : "未填写公司",
                item.CrewBusinessPartnerId,
                BusinessPartnerName = item.CrewBusinessPartner != null ? item.CrewBusinessPartner.Name : item.CrewNameSnapshot ?? "施工班组",
                item.Batch.AccountId,
                AccountName = item.Batch.Account != null ? item.Batch.Account.AccountName : "未填写账户",
                item.Batch.PaymentMethod,
                item.Amount,
                item.Batch.BatchNumber
            })
            .ToListAsync(cancellationToken);
        var payrollKeys = payrollCrewRows.Select(item => new { item.PayrollBatchId, CrewId = item.CrewBusinessPartnerId!.Value }).Distinct().ToArray();
        var payrollBatchIds = payrollKeys.Select(item => item.PayrollBatchId).Distinct().ToArray();
        var payrollAllocations = await db.PayrollCrewAllocations.AsNoTracking()
            .Include(item => item.Contract)
            .Where(item => payrollBatchIds.Contains(item.PayrollBatchId))
            .ToListAsync(cancellationToken);
        payments.AddRange(payrollCrewRows
            .GroupBy(item => new { item.PayrollBatchId, CrewId = item.CrewBusinessPartnerId!.Value })
            .Select(group =>
            {
                var first = group.First();
                var allocation = payrollAllocations.SingleOrDefault(item => item.PayrollBatchId == group.Key.PayrollBatchId && item.CrewBusinessPartnerId == group.Key.CrewId);
                return new ProjectPaymentItemDto(
                    first.Id,
                    first.PaymentDate,
                    allocation?.Contract?.ContractNumber,
                    first.LegalEntityName,
                    first.BusinessPartnerName,
                    first.AccountName,
                    group.Sum(item => item.Amount),
                    first.PaymentMethod,
                    $"民工工资代发 · {first.BatchNumber}",
                    allocation?.PayableEntryId,
                    allocation?.ContractId,
                    first.LegalEntityId,
                    group.Key.CrewId,
                    first.AccountId,
                    default,
                    "PayrollCrewDisbursement",
                    group.Key.PayrollBatchId,
                    first.Id);
            }));
        payments = payments.OrderByDescending(item => item.PaymentDate).ThenBy(item => item.BusinessPartnerName).ToList();

        var overviewEquipment = await db.ProjectConstructionRecords.AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.RecordType == EngineeringManager.Domain.Projects.ProjectConstructionRecordType.Equipment && item.ShowInProjectOverview)
            .OrderByDescending(item => item.EntryDate)
            .ThenBy(item => item.Equipment!.EquipmentNumber)
            .Select(item => new ProjectOverviewEquipmentDto(
                item.Id,
                item.EquipmentId!.Value,
                item.Equipment!.EquipmentNumber,
                item.Equipment.Name,
                item.EntryDate,
                item.ExitDate))
            .ToListAsync(cancellationToken);

        var projectSummary = ProjectSummaryService.Calculate(project);
        var financeSummary = await new FinanceLedgerService(db).GetProjectSummaryAsync(projectId, cancellationToken);
        var activities = await BuildActivitiesAsync(projectId, financeSummary, collections, invoices, payments, cancellationToken);
        return new ProjectWorkspaceDto(
            ToOverview(project),
            projectSummary,
            financeSummary,
            project.Contracts.Where(item => item.IsActive).OrderBy(item => item.ContractNumber).Select(ToContractDto).ToArray(),
            receivables,
            collections,
            invoices,
            payables,
            payments,
            activities,
            project.Milestones.OrderBy(item => item.SortOrder).ThenBy(item => item.PlannedDate).Select(item => new ProjectMilestoneDto(
                item.Id, item.Name, item.PlannedDate, item.ActualDate, item.IsCompleted, item.SortOrder, item.Notes)).ToArray(),
            project.Assignments.OrderBy(item => item.AssignmentType).ThenBy(item => item.User.DisplayName).Select(item => new ProjectAssignmentDto(
                item.Id, item.UserId, item.User.DisplayName, item.AssignmentType, item.IsActive, item.Notes)).ToArray(),
            project.Partners.OrderByDescending(item => item.IsPrimary).ThenBy(item => item.Partner.Name).Select(item => new ProjectPartnerLinkDto(
                item.Id, item.BusinessPartnerId, item.Partner.Name, item.RoleType, item.ContractId, item.Contract == null ? null : item.Contract.ContractNumber,
                item.IsPrimary, item.IsActive, item.Notes)).ToArray(),
            overviewEquipment);
    }

    public async Task<ProjectEditOptionsDto> GetEditOptionsAsync(CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking().Where(item => item.IsEnabled).OrderBy(item => item.DisplayName)
            .Select(item => new ProjectWorkspaceOptionDto(item.Id, item.DisplayName)).ToListAsync(cancellationToken);
        var departments = await db.OrganizationUnits.AsNoTracking().Where(item => item.IsActive && item.UnitType == OrganizationUnitType.Department)
            .OrderBy(item => item.Name).Select(item => new ProjectWorkspaceOptionDto(item.Id.ToString(), item.Name)).ToListAsync(cancellationToken);
        var branches = await db.OrganizationUnits.AsNoTracking().Where(item => item.IsActive && item.UnitType == OrganizationUnitType.Branch)
            .OrderBy(item => item.Name).Select(item => new ProjectWorkspaceOptionDto(item.Id.ToString(), item.Name)).ToListAsync(cancellationToken);
        var legalEntities = await db.LegalEntities.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Code)
            .Select(item => new ProjectWorkspaceOptionDto(item.Id.ToString(), item.Code + " · " + item.ShortName)).ToListAsync(cancellationToken);
        return new ProjectEditOptionsDto(users, departments, branches, legalEntities);
    }

    public async Task<ProjectWorkspaceDto> UpdateAsync(ProjectWorkspaceActor actor, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var reason = Required(request.Reason, nameof(request.Reason));
        var number = Required(request.ProjectNumber, nameof(request.ProjectNumber));
        var name = Required(request.Name, nameof(request.Name));
        var project = await db.Projects.Include(item => item.LegalEntities).Include(item => item.TaxConfigurations)
            .SingleOrDefaultAsync(item => item.Id == request.Id && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("项目不存在或已停用。");
        if (project.ConcurrencyStamp != request.ConcurrencyStamp) throw new DbUpdateConcurrencyException("项目资料已被其他用户修改，请刷新后重试。");
        if (await db.Projects.AnyAsync(item => item.Id != request.Id && item.ProjectNumber == number, cancellationToken))
            throw new InvalidOperationException($"项目编号已存在：{number}");
        ValidateActualDates(request.ActualStartDate, request.ActualCompletionDate);
        ValidateTaxConfigurations(request.TaxConfigurations);
        await ValidateReferencesAsync(request, cancellationToken);

        var previousStage = project.Stage;
        var before = Snapshot(project);
        project.ProjectNumber = number;
        project.Name = name;
        project.ParentProjectName = Optional(request.ParentProjectName);
        project.GeneralContractorName = Optional(request.GeneralContractorName);
        project.GeneralContractorContact = Optional(request.GeneralContractorContact);
        project.GeneralContractorPhone = Optional(request.GeneralContractorPhone);
        project.ResponsibleUserId = Optional(request.ResponsibleUserId);
        project.DepartmentId = request.DepartmentId;
        project.BranchId = request.BranchId;
        project.Stage = request.Stage;
        project.ContractSigningStatus = request.ContractSigningStatus;
        project.AffiliationType = request.AffiliationType;
        project.ActualStartDate = request.ActualStartDate;
        project.ActualCompletionDate = request.ActualCompletionDate;
        project.Notes = Optional(request.Notes);
        project.UpdatedAt = DateTimeOffset.UtcNow;
        db.Entry(project).Property(item => item.ConcurrencyStamp).OriginalValue = request.ConcurrencyStamp;
        project.ConcurrencyStamp = Guid.NewGuid();

        var legalEntityIds = request.LegalEntityIds.Distinct().ToArray();
        var requestedLegalEntityIds = legalEntityIds.ToHashSet();
        var removedLinks = project.LegalEntities.Where(link => !requestedLegalEntityIds.Contains(link.LegalEntityId)).ToArray();
        db.ProjectLegalEntities.RemoveRange(removedLinks);
        foreach (var link in project.LegalEntities.Except(removedLinks))
            link.IsPrimary = legalEntityIds.Length > 0 && link.LegalEntityId == legalEntityIds[0];
        var existingLegalEntityIds = project.LegalEntities.Select(link => link.LegalEntityId).ToHashSet();
        for (var index = 0; index < legalEntityIds.Length; index++)
        {
            if (!existingLegalEntityIds.Contains(legalEntityIds[index]))
            {
                var link = new ProjectLegalEntity { ProjectId = project.Id, LegalEntityId = legalEntityIds[index], IsPrimary = index == 0 };
                project.LegalEntities.Add(link);
                db.ProjectLegalEntities.Add(link);
            }
        }

        if (request.TaxConfigurations is not null)
        {
            var requestedTaxConfigurations = request.TaxConfigurations
                .Select(item => (item.TaxRate, item.InvoiceType))
                .ToHashSet();
            foreach (var configuration in project.TaxConfigurations)
            {
                configuration.IsActive = requestedTaxConfigurations.Contains((configuration.TaxRate, configuration.InvoiceType));
                configuration.UpdatedAt = DateTimeOffset.UtcNow;
                configuration.ConcurrencyStamp = Guid.NewGuid();
            }
            var existingTaxConfigurations = project.TaxConfigurations
                .Select(item => (item.TaxRate, item.InvoiceType))
                .ToHashSet();
            foreach (var configuration in request.TaxConfigurations.Where(item => !existingTaxConfigurations.Contains((item.TaxRate, item.InvoiceType))))
            {
                var newConfiguration = new ProjectTaxConfiguration
                {
                    ProjectId = project.Id,
                    TaxRate = configuration.TaxRate,
                    InvoiceType = configuration.InvoiceType
                };
                project.TaxConfigurations.Add(newConfiguration);
                db.ProjectTaxConfigurations.Add(newConfiguration);
            }
        }

        db.AuditLogs.Add(new AuditLog
        {
            UserId = actor.UserId,
            UserName = actor.UserName,
            Action = "UpdateProject",
            EntityType = nameof(Project),
            EntityId = project.Id.ToString(),
            RelatedProjectId = project.Id.ToString(),
            Reason = reason,
            BeforeJson = JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(Snapshot(project))
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var entityNames = string.Join("、", exception.Entries.Select(entry => entry.Metadata.ClrType.Name).Distinct());
            throw new DbUpdateConcurrencyException($"项目资料保存发生并发冲突：{entityNames}。请刷新后重试。", exception);
        }
        if (previousStage != project.Stage)
        {
            var postingService = new FinancePostingService(db);
            await postingService.SynchronizeProjectQuantityReceivablesAsync(actor.UserId, actor.UserName, project.Id, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(project.Id, cancellationToken) ?? throw new InvalidOperationException("项目保存后无法读取。");
    }

    private async Task ValidateReferencesAsync(UpdateProjectRequest request, CancellationToken token)
    {
        if (!string.IsNullOrWhiteSpace(request.ResponsibleUserId) && !await db.Users.AnyAsync(item => item.Id == request.ResponsibleUserId && item.IsEnabled, token))
            throw new InvalidOperationException("项目负责人不存在或已停用。");
        var organizationIds = new[] { request.DepartmentId, request.BranchId }.Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToArray();
        if (organizationIds.Length > 0 && await db.OrganizationUnits.CountAsync(item => organizationIds.Contains(item.Id) && item.IsActive, token) != organizationIds.Length)
            throw new InvalidOperationException("部门或分支机构不存在或已停用。");
        var legalEntityIds = request.LegalEntityIds.Distinct().ToArray();
        if (legalEntityIds.Length > 0 && await db.LegalEntities.CountAsync(item => legalEntityIds.Contains(item.Id) && item.IsActive, token) != legalEntityIds.Length)
            throw new InvalidOperationException("签约公司不存在或已停用。");
    }

    private async Task<IReadOnlyList<ProjectActivityItemDto>> BuildActivitiesAsync(
        Guid projectId,
        EngineeringManager.Application.Finance.FinanceProjectSummaryDto summary,
        IReadOnlyList<ProjectCollectionItemDto> collections,
        IReadOnlyList<ProjectInvoiceItemDto> invoices,
        IReadOnlyList<ProjectPaymentItemDto> payments,
        CancellationToken token)
    {
        var items = new List<ProjectActivityItemDto>();
        if (summary.UncollectedAmount > 0m) items.Add(new(DateTimeOffset.UtcNow, "经营提示", "存在未收款", $"当前未收款 {summary.UncollectedAmount:N2}", null, "warning"));
        if (summary.UninvoicedAmount > 0m) items.Add(new(DateTimeOffset.UtcNow.AddTicks(-1), "经营提示", "存在未开票", $"当前未开票 {summary.UninvoicedAmount:N2}", null, "warning"));
        if (summary.UnpaidAmount > 0m) items.Add(new(DateTimeOffset.UtcNow.AddTicks(-2), "经营提示", "存在未付款", $"当前未付款 {summary.UnpaidAmount:N2}", null, "warning"));

        var projectLogs = await db.AuditLogs.AsNoTracking()
            .Where(item => item.RelatedProjectId == projectId.ToString() || (item.EntityType == nameof(Project) && item.EntityId == projectId.ToString()))
            .ToListAsync(token);
        var logs = projectLogs.OrderByDescending(item => item.OccurredAt).Take(30);
        items.AddRange(logs.Select(item => new ProjectActivityItemDto(item.OccurredAt, "修改记录", ActionLabel(item.Action), item.Reason, item.UserName, "normal")));
        items.AddRange(collections.Take(8).Select(item => new ProjectActivityItemDto(ToTimestamp(item.CollectionDate), "收款记录", $"收款 {item.Amount:N2}", item.Notes, null, "success")));
        items.AddRange(invoices.Take(8).Select(item => new ProjectActivityItemDto(ToTimestamp(item.InvoiceDate), "发票记录", $"{item.Direction} {item.GrossAmount:N2}", item.InvoiceNumber, null, "normal")));
        items.AddRange(payments.Take(8).Select(item => new ProjectActivityItemDto(ToTimestamp(item.PaymentDate), "付款记录", $"付款 {item.Amount:N2}", item.Notes, null, "normal")));
        return items.OrderByDescending(item => item.OccurredAt).Take(35).ToArray();
    }

    private static ProjectWorkspaceOverviewDto ToOverview(Project project) => new(
        project.Id, project.ProjectNumber, project.Name, project.ParentProjectName, project.GeneralContractorName,
        project.GeneralContractorContact, project.GeneralContractorPhone, project.ResponsibleUserId, project.ResponsibleUser?.DisplayName,
        project.DepartmentId, project.Department?.Name, project.BranchId, project.Branch?.Name, project.Stage, project.AffiliationType,
        project.LegalEntities.OrderByDescending(item => item.IsPrimary).Select(item => new ProjectWorkspaceOptionDto(item.LegalEntityId.ToString(), item.LegalEntity.ShortName)).ToArray(),
        project.UpdatedAt, project.ConcurrencyStamp, project.ActualStartDate, project.ActualCompletionDate, project.Notes,
        project.ContractSigningStatus,
        project.TaxConfigurations.OrderBy(item => item.TaxRate).ThenBy(item => item.InvoiceType)
            .Select(item => new ProjectTaxConfigurationDto(item.Id, item.TaxRate, item.InvoiceType, item.IsActive, item.ConcurrencyStamp)).ToArray());

    private static ContractDto ToContractDto(Contract contract) => new(
        contract.Id, contract.ContractNumber, contract.Name, contract.ContractType, contract.AllocationMode, contract.TotalAmount,
        contract.LineItems.OrderBy(item => item.SortOrder).ThenBy(item => item.Code).Select(item => new ContractLineItemDto(
            item.Id, item.Code, item.Name, item.Unit, item.Quantity, item.UnitPrice,
            (item.Quantity ?? 0m) * (item.UnitPrice ?? 0m), item.Quantity, item.UnitPrice,
            (item.Quantity ?? 0m) * (item.UnitPrice ?? 0m), false, item.ConcurrencyStamp, item.Notes,
            item.Quantity, item.UnitPrice, item.AccountingLabel, item.RequiresInvoice, (item.Quantity ?? 0m) * (item.UnitPrice ?? 0m))).ToArray(), contract.Notes);

    private static object Snapshot(Project item) => new
    {
        item.ProjectNumber, item.Name, item.ParentProjectName, item.GeneralContractorName, item.GeneralContractorContact,
        item.GeneralContractorPhone, item.ResponsibleUserId, item.DepartmentId, item.BranchId, item.Stage, item.ContractSigningStatus, item.AffiliationType,
        item.ActualStartDate, item.ActualCompletionDate, item.Notes,
        LegalEntityIds = item.LegalEntities.Select(link => link.LegalEntityId).Order().ToArray(),
        TaxConfigurations = item.TaxConfigurations.OrderBy(configuration => configuration.TaxRate).ThenBy(configuration => configuration.InvoiceType)
            .Select(configuration => new { configuration.TaxRate, configuration.InvoiceType, configuration.IsActive }).ToArray()
    };

    private static void ValidateTaxConfigurations(IReadOnlyCollection<ProjectTaxConfigurationInput>? configurations)
    {
        if (configurations is null) return;
        if (configurations.Any(item => !ProjectTaxRules.IsAllowedRate(item.TaxRate)))
            throw new ArgumentException("项目税率只允许 1%、3%、6%、9% 或 13%。", nameof(configurations));
        if (configurations.Any(item => !Enum.IsDefined(item.InvoiceType)))
            throw new ArgumentException("项目发票类型无效。", nameof(configurations));
        if (configurations.GroupBy(item => new { item.TaxRate, item.InvoiceType }).Any(group => group.Count() > 1))
            throw new ArgumentException("项目税金配置存在重复的税率和发票类型组合。", nameof(configurations));
    }

    private static void ValidateActualDates(DateOnly? actualStartDate, DateOnly? actualCompletionDate)
    {
        if (actualStartDate.HasValue && actualCompletionDate.HasValue && actualCompletionDate.Value < actualStartDate.Value)
            throw new ArgumentException("实际完工日期不得早于开工日期。");
    }

    private static decimal CurrentSettlementAmount(FinanceSettlement settlement)
    {
        var gross = settlement.OriginalAmount + settlement.Adjustments
            .Where(item => item.Status == LedgerRecordStatus.Active)
            .Sum(item => item.AmountDelta);
        var deductions = settlement.Deductions
            .Where(item => item.Status == LedgerRecordStatus.Active)
            .Sum(item => item.Amount);
        return Math.Max(gross - deductions, 0m);
    }

    private static DateTimeOffset ToTimestamp(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    private static string Required(string value, string parameter) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new ArgumentException("值不能为空。", parameter);
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static PaymentMethod ParsePaymentMethod(string? value) =>
        Enum.TryParse<PaymentMethod>(value, out var method) ? method : PaymentMethod.BankTransfer;
    private static string ActionLabel(string action) => action switch { "UpdateProject" => "编辑项目资料", "CreateReceivable" => "新增应收", "RecordCollection" => "登记收款", "CreatePayable" => "新增应付", "RecordPayment" => "登记付款", "CreateInvoice" => "登记发票", _ => action };
}
