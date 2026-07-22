using EngineeringManager.Application.Finance;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Finance;
using EngineeringManager.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EngineeringManager.Infrastructure.Projects;

public sealed class ProjectService(ApplicationDbContext db) : IProjectService
{
    public async Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var projectNumber = NormalizeRequired(request.ProjectNumber, nameof(request.ProjectNumber));
        var name = NormalizeRequired(request.Name, nameof(request.Name));
        if (await db.Projects.AnyAsync(project => project.ProjectNumber == projectNumber, cancellationToken))
        {
            throw new InvalidOperationException($"项目编号已存在：{projectNumber}");
        }

        await ValidateProjectReferencesAsync(request, cancellationToken);
        ValidateActualDates(request.ActualStartDate, request.ActualCompletionDate);
        ValidateTaxConfigurations(request.TaxConfigurations);
        var project = new Project
        {
            ProjectNumber = projectNumber,
            Name = name,
            ParentProjectName = NormalizeOptional(request.ParentProjectName),
            GeneralContractorName = NormalizeOptional(request.GeneralContractorName),
            GeneralContractorContact = NormalizeOptional(request.GeneralContractorContact),
            GeneralContractorPhone = NormalizeOptional(request.GeneralContractorPhone),
            ResponsibleUserId = request.ResponsibleUserId,
            DepartmentId = request.DepartmentId,
            BranchId = request.BranchId,
            Stage = request.Stage,
            ContractSigningStatus = request.ContractSigningStatus,
            AffiliationType = request.AffiliationType,
            ActualStartDate = request.ActualStartDate,
            ActualCompletionDate = request.ActualCompletionDate,
            Notes = NormalizeOptional(request.Notes)
        };
        foreach (var legalEntityId in request.LegalEntityIds.Distinct())
        {
            project.LegalEntities.Add(new ProjectLegalEntity
            {
                Project = project,
                LegalEntityId = legalEntityId,
                IsPrimary = project.LegalEntities.Count == 0
            });
        }
        foreach (var configuration in request.TaxConfigurations ?? [])
        {
            project.TaxConfigurations.Add(new ProjectTaxConfiguration
            {
                Project = project,
                TaxRate = configuration.TaxRate,
                InvoiceType = configuration.InvoiceType
            });
        }

        project.Contracts.Add(new Contract
        {
            Project = project,
            ContractNumber = BuildProjectContractNumber(projectNumber, 1),
            Name = name,
            ContractType = ContractType.MainContract,
            AllocationMode = ContractAllocationMode.SingleCompany,
            TotalAmount = 0m
        });

        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);
        return ToProjectDto(project);
    }

    public async Task<ContractDto> AddContractAsync(CreateContractRequest request, CancellationToken cancellationToken)
    {
        var contractNumber = NormalizeRequired(request.ContractNumber, nameof(request.ContractNumber));
        var name = NormalizeRequired(request.Name, nameof(request.Name));
        ContractAllocationValidator.Validate(
            request.AllocationMode,
            request.TotalAmount,
            request.Allocations.Select(item => new ContractAllocationInput(item.Amount, item.Percentage)));

        var project = await db.Projects
            .Include(item => item.LegalEntities)
            .SingleOrDefaultAsync(item => item.Id == request.ProjectId && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("项目不存在或已停用。");
        if (await db.Contracts.AnyAsync(
                contract => contract.ProjectId == request.ProjectId && contract.ContractNumber == contractNumber,
                cancellationToken))
        {
            throw new InvalidOperationException($"项目下的合同编号已存在：{contractNumber}");
        }

        var projectLegalEntityIds = project.LegalEntities.Select(item => item.LegalEntityId).ToHashSet();
        if (request.Allocations.Any(item => !projectLegalEntityIds.Contains(item.LegalEntityId)))
        {
            throw new InvalidOperationException("合同分摊公司必须先关联到项目。");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var contract = new Contract
        {
            ProjectId = project.Id,
            ContractNumber = contractNumber,
            Name = name,
            ContractType = request.ContractType,
            AllocationMode = request.AllocationMode,
            CounterpartyName = NormalizeOptional(request.CounterpartyName),
            TotalAmount = request.TotalAmount,
            Notes = NormalizeOptional(request.Notes)
        };
        foreach (var allocation in request.Allocations)
        {
            contract.LegalEntityAllocations.Add(new ContractLegalEntityAllocation
            {
                Contract = contract,
                LegalEntityId = allocation.LegalEntityId,
                Amount = allocation.Amount,
                Percentage = allocation.Percentage
            });
        }

        db.Contracts.Add(contract);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToContractDto(contract);
    }

    public async Task<ContractLineItemDto> AddLineItemAsync(
        CreateContractLineItemRequest request,
        CancellationToken cancellationToken)
    {
        var code = NormalizeRequired(request.Code, nameof(request.Code));
        var name = NormalizeRequired(request.Name, nameof(request.Name));
        var unit = NormalizeRequired(request.Unit, nameof(request.Unit));
        if (!await db.Contracts.AnyAsync(contract => contract.Id == request.ContractId && contract.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("合同不存在或已停用。");
        }

        if (await db.ContractLineItems.AnyAsync(
                item => item.ContractId == request.ContractId && item.Code == code,
                cancellationToken))
        {
            throw new InvalidOperationException($"合同下的清单编码已存在：{code}");
        }

        var lineItem = new ContractLineItem
        {
            ContractId = request.ContractId,
            Code = code,
            Name = name,
            Unit = unit,
            Quantity = request.Quantity ?? (request.IsSettlementConfirmed ? request.SettledQuantity : request.EstimatedQuantity),
            UnitPrice = request.UnitPrice ?? (request.IsSettlementConfirmed ? request.SettledUnitPrice : request.EstimatedUnitPrice),
            AccountingLabel = NormalizeOptional(request.AccountingLabel),
            RequiresInvoice = request.RequiresInvoice,
            Notes = NormalizeOptional(request.Notes)
        };
        db.ContractLineItems.Add(lineItem);
        await db.SaveChangesAsync(cancellationToken);
        await PostLineItemIfConfiguredAsync(lineItem.Id, cancellationToken);
        return ToLineItemDto(lineItem);
    }

    public async Task<ContractLineItemDto> UpdateLineItemAsync(
        UpdateContractLineItemRequest request,
        CancellationToken cancellationToken)
    {
        var code = NormalizeRequired(request.Code, nameof(request.Code));
        var name = NormalizeRequired(request.Name, nameof(request.Name));
        var unit = NormalizeRequired(request.Unit, nameof(request.Unit));
        var lineItem = await db.ContractLineItems.Include(item => item.Contract).SingleOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("工程量明细不存在。");
        if (lineItem.ConcurrencyStamp != request.ConcurrencyStamp)
        {
            throw new DbUpdateConcurrencyException("工程量明细已被其他用户修改，请刷新后重试。");
        }
        if (await db.ContractLineItems.AnyAsync(item => item.ContractId == lineItem.ContractId && item.Id != lineItem.Id && item.Code == code, cancellationToken))
        {
            throw new InvalidOperationException($"合同下的清单编码已存在：{code}");
        }

        var before = JsonSerializer.Serialize(LineItemSnapshot(lineItem));
        lineItem.Code = code;
        lineItem.Name = name;
        lineItem.Unit = unit;
        lineItem.Quantity = request.Quantity ?? (request.IsSettlementConfirmed ? request.SettledQuantity : request.EstimatedQuantity);
        lineItem.UnitPrice = request.UnitPrice ?? (request.IsSettlementConfirmed ? request.SettledUnitPrice : request.EstimatedUnitPrice);
        lineItem.AccountingLabel = NormalizeOptional(request.AccountingLabel);
        lineItem.RequiresInvoice = request.RequiresInvoice;
        lineItem.Notes = NormalizeOptional(request.Notes);
        lineItem.ConcurrencyStamp = Guid.NewGuid();
        db.AuditLogs.Add(new AuditLog
        {
            UserId = NormalizeOptional(request.UserId),
            Action = "UpdateContractLineItem",
            EntityType = nameof(ContractLineItem),
            EntityId = lineItem.Id.ToString(),
            RelatedProjectId = lineItem.Contract.ProjectId.ToString(),
            Reason = NormalizeOptional(request.Reason) ?? "修改工程量明细",
            BeforeJson = before,
            AfterJson = JsonSerializer.Serialize(LineItemSnapshot(lineItem))
        });
        await db.SaveChangesAsync(cancellationToken);
        await PostLineItemIfConfiguredAsync(lineItem.Id, cancellationToken);
        return ToLineItemDto(lineItem);
    }

    public async Task<IReadOnlyList<ProjectListItemDto>> ListProjectsAsync(
        string? search,
        ProjectStage? stage,
        CancellationToken cancellationToken)
    {
        var query = db.Projects.AsNoTracking()
            .Include(project => project.Contracts).ThenInclude(contract => contract.LineItems)
            .Include(project => project.TaxConfigurations)
            .Where(project => project.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(project => project.ProjectNumber.Contains(term) || project.Name.Contains(term));
        }
        if (stage.HasValue) query = query.Where(project => project.Stage == stage.Value);
        var projects = await query.OrderByDescending(project => project.CreatedAt).ToListAsync(cancellationToken);
        return projects.Select(project => new ProjectListItemDto(ToProjectDto(project), ProjectSummaryService.Calculate(project))).ToArray();
    }

    public async Task<ProjectListPageDto> SearchProjectsAsync(
        ProjectListActor actor,
        ProjectListQuery query,
        CancellationToken cancellationToken)
    {
        var projectQuery = db.Projects
            .AsNoTracking()
            .AsSplitQuery()
            .Include(project => project.Contracts)
                .ThenInclude(contract => contract.LineItems)
            .Include(project => project.TaxConfigurations)
            .Include(project => project.ResponsibleUser)
            .Include(project => project.Department)
            .Include(project => project.Branch)
            .Include(project => project.LegalEntities)
                .ThenInclude(link => link.LegalEntity)
            .Where(project => query.IncludeInactive || project.IsActive);
        if (!actor.CanAccessAllProjects)
        {
            projectQuery = projectQuery.Where(project => project.ResponsibleUserId == actor.UserId || project.Assignments.Any(assignment => assignment.UserId == actor.UserId));
        }

        foreach (var term in SearchTerms.Parse(query.Search))
        {
            var stage = ParseStage(term);
            var signingStatus = ParseSigningStatus(term);
            var affiliation = ParseAffiliationType(term);
            var hasDate = SearchTerms.TryParseDate(term, out var date);
            var hasAmount = SearchTerms.TryParseDecimal(term, out var amount);
            projectQuery = projectQuery.Where(project =>
                project.ProjectNumber.Contains(term)
                || project.Name.Contains(term)
                || (project.ParentProjectName != null && project.ParentProjectName.Contains(term))
                || (project.GeneralContractorName != null && project.GeneralContractorName.Contains(term))
                || (project.GeneralContractorContact != null && project.GeneralContractorContact.Contains(term))
                || (project.GeneralContractorPhone != null && project.GeneralContractorPhone.Contains(term))
                || (project.Notes != null && project.Notes.Contains(term))
                || (project.ResponsibleUser != null && (project.ResponsibleUser.UserName!.Contains(term) || project.ResponsibleUser.DisplayName.Contains(term)))
                || (project.Department != null && (project.Department.Code.Contains(term) || project.Department.Name.Contains(term)))
                || (project.Branch != null && (project.Branch.Code.Contains(term) || project.Branch.Name.Contains(term)))
                || project.LegalEntities.Any(link => link.LegalEntity.Code.Contains(term) || link.LegalEntity.Name.Contains(term) || link.LegalEntity.ShortName.Contains(term))
                || project.Partners.Any(link => (link.Notes != null && link.Notes.Contains(term)) || link.Partner.PartnerNumber.Contains(term) || link.Partner.Name.Contains(term) || link.Partner.ShortName.Contains(term))
                || project.Assignments.Any(assignment => (assignment.Notes != null && assignment.Notes.Contains(term)) || assignment.User.DisplayName.Contains(term))
                || project.Contracts.Any(contract =>
                    contract.ContractNumber.Contains(term)
                    || contract.Name.Contains(term)
                    || (contract.CounterpartyName != null && contract.CounterpartyName.Contains(term))
                    || (contract.Notes != null && contract.Notes.Contains(term))
                    || (contract.BusinessPartner != null && (contract.BusinessPartner.Name.Contains(term) || contract.BusinessPartner.ShortName.Contains(term)))
                    || (hasDate && contract.SignedDate == date)
                    || (hasAmount && contract.TotalAmount == amount)
                    || contract.LineItems.Any(line => line.Code.Contains(term) || line.Name.Contains(term) || line.Unit.Contains(term) || (line.Notes != null && line.Notes.Contains(term))))
                || project.TaxConfigurations.Any(tax => hasAmount && tax.TaxRate == amount)
                || (stage.HasValue && project.Stage == stage.Value)
                || (signingStatus.HasValue && project.ContractSigningStatus == signingStatus.Value)
                || (affiliation.HasValue && project.AffiliationType == affiliation.Value)
                || (hasDate && (project.ActualStartDate == date || project.ActualCompletionDate == date)));
        }
        if (query.Stages.Count > 0)
        {
            var stages = query.Stages.Distinct().ToArray();
            projectQuery = projectQuery.Where(project => stages.Contains(project.Stage));
        }
        if (query.LegalEntityId.HasValue)
        {
            projectQuery = projectQuery.Where(project => project.LegalEntities.Any(item => item.LegalEntityId == query.LegalEntityId.Value));
        }
        if (query.AffiliationType.HasValue)
        {
            projectQuery = projectQuery.Where(project => project.AffiliationType == query.AffiliationType.Value);
        }
        if (!string.IsNullOrWhiteSpace(query.ResponsibleUserId))
        {
            var responsibleUserId = query.ResponsibleUserId.Trim();
            projectQuery = projectQuery.Where(project => project.ResponsibleUserId == responsibleUserId);
        }

        var projects = await projectQuery.ToListAsync(cancellationToken);
        IEnumerable<ProjectListItemDto> items = projects.Select(project => new ProjectListItemDto(ToProjectDto(project), ProjectSummaryService.Calculate(project)));
        if (query.MinimumCurrentAmount.HasValue) items = items.Where(item => item.Summary.CurrentAmount >= query.MinimumCurrentAmount.Value);
        if (query.MaximumCurrentAmount.HasValue) items = items.Where(item => item.Summary.CurrentAmount <= query.MaximumCurrentAmount.Value);
        items = SortProjects(items, query.SortKey, query.SortDescending);

        var matching = items.ToArray();
        var pageSize = NormalizePageSize(query.PageSize);
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)matching.Length / pageSize));
        var page = Math.Clamp(query.Page, 1, totalPages);
        return new ProjectListPageDto(
            matching.Skip((page - 1) * pageSize).Take(pageSize).ToArray(),
            new ProjectListAggregateDto(
                matching.Length,
                matching.Sum(item => item.Summary.ContractAmount),
                matching.Sum(item => item.Summary.CurrentAmount),
                matching.Count(item => item.Summary.SettlementStatus == ProjectSettlementStatus.Settled)),
            page,
            pageSize,
            matching.Length,
            totalPages,
            matching.Select(item => item.Project.Id).ToArray());
    }

    public async Task<ProjectListOptionsDto> GetListOptionsAsync(ProjectListActor actor, CancellationToken cancellationToken)
    {
        var query = db.Projects.AsNoTracking().Where(item => item.IsActive);
        if (!actor.CanAccessAllProjects)
        {
            query = query.Where(item => item.ResponsibleUserId == actor.UserId || item.Assignments.Any(assignment => assignment.UserId == actor.UserId));
        }
        var projectIds = query.Select(item => item.Id);
        var legalEntityRows = await db.ProjectLegalEntities.AsNoTracking()
            .Where(item => projectIds.Contains(item.ProjectId))
            .Select(item => new { item.LegalEntityId, item.LegalEntity.ShortName })
            .ToListAsync(cancellationToken);
        var legalEntities = legalEntityRows
            .DistinctBy(item => item.LegalEntityId)
            .Select(item => new ProjectFilterOptionDto(item.LegalEntityId.ToString(), item.ShortName))
            .OrderBy(item => item.Label)
            .ToArray();
        var responsibleRows = await query.Where(item => item.ResponsibleUserId != null && item.ResponsibleUser != null)
            .Select(item => new { UserId = item.ResponsibleUserId!, item.ResponsibleUser!.DisplayName })
            .ToListAsync(cancellationToken);
        var responsibleUsers = responsibleRows
            .DistinctBy(item => item.UserId)
            .Select(item => new ProjectFilterOptionDto(item.UserId, item.DisplayName))
            .OrderBy(item => item.Label)
            .ToArray();
        return new ProjectListOptionsDto(legalEntities, responsibleUsers);
    }

    public async Task<ProjectDetailsDto?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await db.Projects
            .AsNoTracking()
            .Include(item => item.Contracts)
                .ThenInclude(contract => contract.LineItems)
            .Include(item => item.TaxConfigurations)
            .SingleOrDefaultAsync(item => item.Id == projectId && item.IsActive, cancellationToken);
        if (project is null)
        {
            return null;
        }

        return new ProjectDetailsDto(
            ToProjectDto(project),
            ProjectSummaryService.Calculate(project),
            project.Contracts.Where(contract => contract.IsActive).Select(ToContractDto).ToArray());
    }

    private async Task ValidateProjectReferencesAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        if (request.ResponsibleUserId is not null && !await db.Users.AnyAsync(user => user.Id == request.ResponsibleUserId && user.IsEnabled, cancellationToken))
        {
            throw new InvalidOperationException("项目负责人不存在或已停用。");
        }

        var organizationIds = new[] { request.DepartmentId, request.BranchId }.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (organizationIds.Length > 0 && await db.OrganizationUnits.CountAsync(unit => organizationIds.Contains(unit.Id) && unit.IsActive, cancellationToken) != organizationIds.Length)
        {
            throw new InvalidOperationException("项目部门或分支机构不存在或已停用。");
        }

        var legalEntityIds = request.LegalEntityIds.Distinct().ToArray();
        if (legalEntityIds.Length > 0 && await db.LegalEntities.CountAsync(item => legalEntityIds.Contains(item.Id) && item.IsActive, cancellationToken) != legalEntityIds.Length)
        {
            throw new InvalidOperationException("项目关联的签约公司不存在或已停用。");
        }
    }

    private static ProjectDto ToProjectDto(Project project) =>
        new(project.Id, project.ProjectNumber, project.Name, project.GeneralContractorName, project.Stage,
            project.AffiliationType, project.ActualStartDate, project.ActualCompletionDate, project.Notes, project.ContractSigningStatus,
            project.TaxConfigurations.OrderBy(item => item.TaxRate).ThenBy(item => item.InvoiceType)
                .Select(item => new ProjectTaxConfigurationDto(item.Id, item.TaxRate, item.InvoiceType, item.IsActive, item.ConcurrencyStamp)).ToArray(),
            project.ParentProjectName,
            project.GeneralContractorContact,
            project.GeneralContractorPhone,
            project.ResponsibleUserId,
            project.ResponsibleUser?.DisplayName,
            project.Department?.Name,
            project.Branch?.Name,
            project.LegalEntities.OrderByDescending(item => item.IsPrimary).Select(item => item.LegalEntity?.ShortName).Where(name => !string.IsNullOrWhiteSpace(name)).Cast<string>().ToArray());

    private static string BuildProjectContractNumber(string projectNumber, int sequence)
    {
        var suffix = $"-C{sequence:00}";
        var maxPrefixLength = Math.Max(1, 80 - suffix.Length);
        var prefix = projectNumber.Length <= maxPrefixLength
            ? projectNumber
            : projectNumber[..maxPrefixLength];
        return prefix + suffix;
    }

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

    private static ContractDto ToContractDto(Contract contract) =>
        new(
            contract.Id,
            contract.ContractNumber,
            contract.Name,
            contract.ContractType,
            contract.AllocationMode,
            contract.TotalAmount,
            contract.LineItems.OrderBy(item => item.SortOrder).ThenBy(item => item.Code).Select(ToLineItemDto).ToArray(),
            contract.Notes, contract.BusinessPartnerId, contract.BusinessPartner?.Name, contract.ConcurrencyStamp);

    private static ContractLineItemDto ToLineItemDto(ContractLineItem item)
    {
        var amount = (item.Quantity ?? 0m) * (item.UnitPrice ?? 0m);
        return new ContractLineItemDto(
            item.Id,
            item.Code,
            item.Name,
            item.Unit,
            item.Quantity,
            item.UnitPrice,
            amount,
            item.Quantity,
            item.UnitPrice,
            amount,
            false,
            item.ConcurrencyStamp,
            item.Notes,
            item.Quantity,
            item.UnitPrice,
            item.AccountingLabel,
            item.RequiresInvoice,
            amount);
    }

    private static object LineItemSnapshot(ContractLineItem item) => new
    {
        item.ContractId,
        item.Code,
        item.Name,
        item.Unit,
        item.Quantity,
        item.UnitPrice,
        item.AccountingLabel,
        item.RequiresInvoice,
        item.Notes,
        item.ConcurrencyStamp
    };

    private async Task PostLineItemIfConfiguredAsync(Guid lineItemId, CancellationToken token)
    {
        var lineItem = await db.ContractLineItems.AsNoTracking()
            .Include(item => item.Contract).ThenInclude(item => item.LegalEntityAllocations)
            .Include(item => item.Contract).ThenInclude(item => item.Project).ThenInclude(item => item.LegalEntities)
            .SingleAsync(item => item.Id == lineItemId, token);
        var legalEntityId = lineItem.Contract.LegalEntityAllocations
            .OrderByDescending(item => item.Amount ?? 0m)
            .ThenByDescending(item => item.Percentage ?? 0m)
            .Select(item => (Guid?)item.LegalEntityId)
            .FirstOrDefault()
            ?? lineItem.Contract.Project.LegalEntities.OrderByDescending(item => item.IsPrimary)
                .Select(item => (Guid?)item.LegalEntityId)
                .FirstOrDefault();
        if (!legalEntityId.HasValue || !lineItem.Contract.BusinessPartnerId.HasValue)
        {
            return;
        }

        var actor = new CentralLedgerActor(
            "project-service",
            "项目管理",
            new HashSet<Guid> { legalEntityId.Value },
            new HashSet<Guid> { lineItem.Contract.ProjectId },
            true,
            false,
            false,
            false);
        await new FinancePostingService(db).UpsertProjectQuantityReceivableAsync(actor, lineItemId, token);
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("值不能为空。", parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ProjectStage? ParseStage(string term) => term switch
    {
        "待进场" => ProjectStage.AwaitingMobilization,
        "施工中" => ProjectStage.UnderConstruction,
        "停工中" => ProjectStage.Suspended,
        "已完工未结算" => ProjectStage.CompletedUnsettled,
        "部分结算" => ProjectStage.PartiallySettled,
        "已结算归档" => ProjectStage.SettledArchived,
        _ => Enum.TryParse<ProjectStage>(term, true, out var value) ? value : null
    };

    private static ContractSigningStatus? ParseSigningStatus(string term) => term switch
    {
        "未签合同" or "未签" => ContractSigningStatus.NotSigned,
        "合同已寄出" or "已寄出" => ContractSigningStatus.SentForSignature,
        "合同已签完" or "已签完" => ContractSigningStatus.FullySigned,
        _ => Enum.TryParse<ContractSigningStatus>(term, true, out var value) ? value : null
    };

    private static ProjectAffiliationType? ParseAffiliationType(string term) => term switch
    {
        "自营项目" or "自营" => ProjectAffiliationType.SelfOperated,
        "他方挂靠我方" => ProjectAffiliationType.ExternalPartyAttachedToUs,
        "我方挂靠他方" => ProjectAffiliationType.WeAttachedToExternalParty,
        _ => Enum.TryParse<ProjectAffiliationType>(term, true, out var value) ? value : null
    };

    private static IEnumerable<ProjectListItemDto> SortProjects(IEnumerable<ProjectListItemDto> items, string? sortKey, bool descending)
    {
        Func<ProjectListItemDto, object> selector = sortKey switch
        {
            "Name" => item => item.Project.Name,
            "Stage" => item => item.Project.Stage,
            "ContractAmount" => item => item.Summary.ContractAmount,
            "CurrentAmount" => item => item.Summary.CurrentAmount,
            "SettlementStatus" => item => item.Summary.SettlementStatus,
            _ => item => item.Project.ProjectNumber
        };
        return descending ? items.OrderByDescending(selector).ThenBy(item => item.Project.ProjectNumber) : items.OrderBy(selector).ThenBy(item => item.Project.ProjectNumber);
    }

    private static int NormalizePageSize(int pageSize) => pageSize is 20 or 50 or 100 ? pageSize : 20;
}
