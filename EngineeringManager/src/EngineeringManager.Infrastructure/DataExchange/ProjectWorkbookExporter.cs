using EngineeringManager.Application.DataExchange;
using EngineeringManager.Application.Finance;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Projects;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using EngineeringManager.Infrastructure.Files;

namespace EngineeringManager.Infrastructure.DataExchange;

public sealed class ProjectWorkbookExporter(
    ApplicationDbContext db,
    IProjectService projectService,
    IFinanceLedgerService financeService,
    IFileStore? fileStore = null)
{
    public async Task<(byte[] Content, int ProjectCount, int RowCount, IReadOnlyList<Guid> ProjectIds, bool IsArchive)> ExportAsync(
        ProjectWorkbookExportRequest request,
        CancellationToken cancellationToken)
    {
        var projectIds = await ResolveProjectIdsAsync(request.Scope, cancellationToken);
        var selectedSheets = ResolveSheets(request.Sheets, request.IncludeAttachments);
        var projects = await db.Projects
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.Contracts).ThenInclude(item => item.LineItems)
            .Include(item => item.ResponsibleUser)
            .Include(item => item.Department)
            .Include(item => item.Branch)
            .Include(item => item.LegalEntities).ThenInclude(item => item.LegalEntity)
            .Where(item => projectIds.Contains(item.Id))
            .OrderBy(item => item.ProjectNumber)
            .ToListAsync(cancellationToken);
        var attachments = request.IncludeAttachments ? await AttachmentQuery(projectIds, cancellationToken) : [];
        var attachmentHashes = request.IncludeAttachments ? await ComputeAttachmentHashesAsync(attachments, cancellationToken) : new Dictionary<Guid, string>();

        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet("目录说明", ["项目工作簿版本", "生成时间", "项目数量", "筛选关键词", "选中项目ID", "工作表"],
            [[ProjectWorkbookVersions.Workbook, DateTimeOffset.UtcNow, projects.Count, request.Scope.Query.Search, string.Join(",", projectIds), string.Join("、", selectedSheets.Select(item => ProjectWorkbookCatalog.Get(item).WorksheetName))]]);
        workbook.AddWorksheet("_metadata", ["WorkbookVersion", "DatasetVersion", "SelectedSheets"],
            [[ProjectWorkbookVersions.Workbook, ProjectWorkbookVersions.Dataset, string.Join(",", selectedSheets)]],
            new XlsxWorksheetOptions([], ProtectSheet: true, HiddenSheet: true));

        var rowCount = 0;
        foreach (var sheet in selectedSheets)
        {
            var definition = ProjectWorkbookCatalog.Get(sheet);
            var fields = definition.Fields.Where(item => item.CanExport).ToArray();
            var rows = sheet switch
            {
                ProjectWorkbookSheet.ProjectMaster => ProjectRows(projects, fields),
                ProjectWorkbookSheet.ProjectSummary => await SummaryRowsAsync(projects, fields, request.CutoffDate, cancellationToken),
                ProjectWorkbookSheet.Contracts => ContractRows(projects, fields),
                ProjectWorkbookSheet.QuantityLines => QuantityRows(projects, fields),
                ProjectWorkbookSheet.Milestones => MilestoneRows(await db.ProjectMilestones.AsNoTracking().Include(item => item.Project).Where(item => projectIds.Contains(item.ProjectId)).OrderBy(item => item.Project.ProjectNumber).ThenBy(item => item.SortOrder).ToListAsync(cancellationToken), fields),
                ProjectWorkbookSheet.Assignments => AssignmentRows(await db.ProjectAssignments.AsNoTracking().Include(item => item.Project).Include(item => item.User).Where(item => projectIds.Contains(item.ProjectId)).OrderBy(item => item.Project.ProjectNumber).ThenBy(item => item.UserId).ToListAsync(cancellationToken), fields),
                ProjectWorkbookSheet.Partners => PartnerRows(await db.ProjectPartners.AsNoTracking().Include(item => item.Project).Include(item => item.Partner).Include(item => item.Contract).Where(item => projectIds.Contains(item.ProjectId)).OrderBy(item => item.Project.ProjectNumber).ThenBy(item => item.Partner.PartnerNumber).ToListAsync(cancellationToken), fields),
                ProjectWorkbookSheet.Construction => ConstructionRows(await db.ProjectConstructionRecords.AsNoTracking().Include(item => item.Project).Include(item => item.Equipment).Include(item => item.CrewBusinessPartner).Include(item => item.TransferFromProject).Include(item => item.TransferToProject).Where(item => projectIds.Contains(item.ProjectId)).OrderBy(item => item.Project.ProjectNumber).ThenBy(item => item.EntryDate).ToListAsync(cancellationToken), fields),
                ProjectWorkbookSheet.StageResults => StageResultRows(await db.StageResults.AsNoTracking().Include(item => item.Project).Include(item => item.Contract).Where(item => projectIds.Contains(item.ProjectId)).OrderBy(item => item.Project.ProjectNumber).ThenBy(item => item.ResultDate).ToListAsync(cancellationToken), fields),
                ProjectWorkbookSheet.Receivables => await SettlementRowsAsync(projectIds, LedgerDirection.Receivable, fields, cancellationToken),
                ProjectWorkbookSheet.Collections => await CashRowsAsync(projectIds, LedgerDirection.Receivable, fields, cancellationToken),
                ProjectWorkbookSheet.Payables => await SettlementRowsAsync(projectIds, LedgerDirection.Payable, fields, cancellationToken),
                ProjectWorkbookSheet.Payments => await CashRowsAsync(projectIds, LedgerDirection.Payable, fields, cancellationToken),
                ProjectWorkbookSheet.Invoices => await CentralInvoiceRowsAsync(projectIds, fields, cancellationToken),
                ProjectWorkbookSheet.Deductions => await DeductionRowsAsync(projectIds, fields, cancellationToken),
                ProjectWorkbookSheet.Attachments => AttachmentRows(attachments, fields, attachmentHashes),
                _ => Array.Empty<IReadOnlyList<object?>>()
            };
            var materialized = rows.ToArray();
            rowCount += materialized.Length;
            workbook.AddWorksheet(definition.WorksheetName, fields.Select(item => item.Header).ToArray(), materialized,
                new XlsxWorksheetOptions(fields.Select((field, index) => (field, index)).Where(item => item.field.IsHidden).Select(item => item.index).ToArray(), ProtectSheet: true));
        }

        var workbookBytes = workbook.ToArray();
        if (request.IncludeAttachments)
        {
            if (fileStore is null) throw new InvalidOperationException("导出附件需要文件存储。");
            return (await new ProjectWorkbookArchive(fileStore).CreateAsync(workbookBytes, attachments, cancellationToken), projects.Count, rowCount, projectIds, true);
        }
        return (workbookBytes, projects.Count, rowCount, projectIds, false);
    }

    private async Task<IReadOnlyList<Guid>> ResolveProjectIdsAsync(ProjectWorkbookScope scope, CancellationToken cancellationToken)
    {
        var result = await projectService.SearchProjectsAsync(scope.Actor, scope.Query with { Page = 1, PageSize = 100 }, cancellationToken);
        var matching = result.MatchingProjectIds.ToHashSet();
        var ids = scope.SelectAllMatching
            ? matching
            : (scope.SelectedProjectIds ?? []).Where(matching.Contains).ToHashSet();
        if (ids.Count == 0) throw new InvalidOperationException("没有可导出的项目。");
        return ids.OrderBy(item => item).ToArray();
    }

    private static ProjectWorkbookSheet[] ResolveSheets(IReadOnlyCollection<ProjectWorkbookSheet> requested, bool includeAttachments)
    {
        var selected = requested.Count == 0
            ? ProjectWorkbookCatalog.Sheets.Where(item => item.Sheet != ProjectWorkbookSheet.Attachments).Select(item => item.Sheet).ToHashSet()
            : requested.ToHashSet();
        if (includeAttachments) selected.Add(ProjectWorkbookSheet.Attachments);
        if (selected.Contains(ProjectWorkbookSheet.Attachments)) selected.Add(ProjectWorkbookSheet.ProjectMaster);
        return ProjectWorkbookCatalog.Sheets.Where(item => selected.Contains(item.Sheet)).Select(item => item.Sheet).ToArray();
    }

    private static IEnumerable<IReadOnlyList<object?>> ProjectRows(List<Project> projects, IReadOnlyList<ProjectWorkbookFieldDefinition> fields) =>
        projects.Select(project => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["project_number"] = project.ProjectNumber, ["project_name"] = project.Name, ["parent_project"] = project.ParentProjectName,
            ["general_contractor"] = project.GeneralContractorName, ["general_contractor_contact"] = project.GeneralContractorContact, ["general_contractor_phone"] = project.GeneralContractorPhone,
            ["responsible_user_id"] = project.ResponsibleUserId, ["responsible_user"] = project.ResponsibleUser?.DisplayName, ["department_id"] = project.DepartmentId?.ToString(), ["department"] = project.Department?.Name,
            ["branch_id"] = project.BranchId?.ToString(), ["branch"] = project.Branch?.Name, ["stage"] = project.Stage.ToString(), ["contract_signing_status"] = project.ContractSigningStatus.ToString(),
            ["affiliation_type"] = project.AffiliationType.ToString(), ["legal_entity_ids"] = string.Join(",", project.LegalEntities.Select(item => item.LegalEntityId)), ["legal_entities"] = string.Join("、", project.LegalEntities.Select(item => item.LegalEntity.ShortName)),
            ["actual_start_date"] = project.ActualStartDate, ["actual_completion_date"] = project.ActualCompletionDate, ["is_active"] = project.IsActive, ["notes"] = project.Notes,
            ["_system_id"] = project.Id.ToString(), ["_project_system_id"] = project.Id.ToString(), ["_concurrency_stamp"] = project.ConcurrencyStamp.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
        }));

    private static IEnumerable<IReadOnlyList<object?>> ContractRows(List<Project> projects, IReadOnlyList<ProjectWorkbookFieldDefinition> fields) =>
        projects.SelectMany(project => project.Contracts.OrderBy(item => item.ContractNumber).Select(contract => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["project_number"] = project.ProjectNumber, ["contract_number"] = contract.ContractNumber, ["name"] = contract.Name, ["contract_type"] = contract.ContractType.ToString(), ["allocation_mode"] = contract.AllocationMode.ToString(), ["counterparty_name"] = contract.CounterpartyName, ["signed_date"] = contract.SignedDate, ["total_amount"] = contract.TotalAmount, ["is_active"] = contract.IsActive, ["notes"] = contract.Notes,
            ["_system_id"] = contract.Id.ToString(), ["_project_system_id"] = project.Id.ToString(), ["_concurrency_stamp"] = contract.ConcurrencyStamp.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
        })));

    private static IEnumerable<IReadOnlyList<object?>> QuantityRows(List<Project> projects, IReadOnlyList<ProjectWorkbookFieldDefinition> fields) =>
        projects.SelectMany(project => project.Contracts.SelectMany(contract => contract.LineItems.OrderBy(item => item.Code).Select(line => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["project_number"] = project.ProjectNumber, ["contract_number"] = contract.ContractNumber, ["code"] = line.Code, ["name"] = line.Name, ["unit"] = line.Unit, ["quantity"] = line.Quantity, ["unit_price"] = line.UnitPrice, ["accounting_label"] = line.AccountingLabel, ["requires_invoice"] = line.RequiresInvoice, ["notes"] = line.Notes,
            ["_system_id"] = line.Id.ToString(), ["_project_system_id"] = project.Id.ToString(), ["_contract_system_id"] = contract.Id.ToString(), ["_concurrency_stamp"] = line.ConcurrencyStamp.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
        }))));

    private static IEnumerable<IReadOnlyList<object?>> MilestoneRows(List<ProjectMilestone> items, IReadOnlyList<ProjectWorkbookFieldDefinition> fields) =>
        items.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["project_number"] = item.Project.ProjectNumber, ["name"] = item.Name, ["planned_date"] = item.PlannedDate, ["actual_date"] = item.ActualDate, ["is_completed"] = item.IsCompleted, ["sort_order"] = item.SortOrder, ["notes"] = item.Notes,
            ["_system_id"] = item.Id.ToString(), ["_project_system_id"] = item.ProjectId.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
        }));

    private static IEnumerable<IReadOnlyList<object?>> AssignmentRows(List<ProjectAssignment> items, IReadOnlyList<ProjectWorkbookFieldDefinition> fields) =>
        items.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["project_number"] = item.Project.ProjectNumber, ["user_id"] = item.UserId, ["user_name"] = item.User.DisplayName, ["assignment_type"] = item.AssignmentType.ToString(), ["is_active"] = item.IsActive, ["notes"] = item.Notes,
            ["_system_id"] = item.Id.ToString(), ["_project_system_id"] = item.ProjectId.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
        }));

    private static IEnumerable<IReadOnlyList<object?>> PartnerRows(List<ProjectPartner> items, IReadOnlyList<ProjectWorkbookFieldDefinition> fields) =>
        items.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["project_number"] = item.Project.ProjectNumber, ["partner_number"] = item.Partner.PartnerNumber, ["partner_name"] = item.Partner.Name, ["role_type"] = item.RoleType.ToString(), ["contract_number"] = item.Contract?.ContractNumber, ["is_primary"] = item.IsPrimary, ["is_active"] = item.IsActive, ["notes"] = item.Notes,
            ["_system_id"] = item.Id.ToString(), ["_project_system_id"] = item.ProjectId.ToString(), ["_contract_system_id"] = item.ContractId?.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
        }));

    private static IEnumerable<IReadOnlyList<object?>> ConstructionRows(List<ProjectConstructionRecord> items, IReadOnlyList<ProjectWorkbookFieldDefinition> fields) =>
        items.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["project_number"] = item.Project.ProjectNumber, ["record_type"] = item.RecordType.ToString(), ["equipment_number"] = item.Equipment?.EquipmentNumber, ["crew_partner_number"] = item.CrewBusinessPartner?.PartnerNumber,
            ["transfer_from_project_number"] = item.TransferFromProject?.ProjectNumber, ["transfer_to_project_number"] = item.TransferToProject?.ProjectNumber, ["entry_date"] = item.EntryDate, ["exit_date"] = item.ExitDate, ["stop_days"] = item.StopDays, ["is_draft"] = item.IsDraft, ["show_in_project_overview"] = item.ShowInProjectOverview, ["notes"] = item.Notes,
            ["_system_id"] = item.Id.ToString(), ["_project_system_id"] = item.ProjectId.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
        }));

    private static IEnumerable<IReadOnlyList<object?>> StageResultRows(List<StageResult> items, IReadOnlyList<ProjectWorkbookFieldDefinition> fields) =>
        items.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["project_number"] = item.Project.ProjectNumber, ["contract_number"] = item.Contract?.ContractNumber, ["title"] = item.Title, ["result_type"] = item.ResultType.ToString(), ["status"] = item.Status.ToString(), ["result_date"] = item.ResultDate, ["quality_result"] = item.QualityResult.ToString(), ["description"] = item.Description,
            ["_system_id"] = item.Id.ToString(), ["_project_system_id"] = item.ProjectId.ToString(), ["_contract_system_id"] = item.ContractId?.ToString(), ["_concurrency_stamp"] = item.ConcurrencyStamp.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
        }));

    private async Task<IEnumerable<IReadOnlyList<object?>>> SettlementRowsAsync(IReadOnlyList<Guid> projectIds, LedgerDirection direction, IReadOnlyList<ProjectWorkbookFieldDefinition> fields, CancellationToken token)
    {
        var items = await db.FinanceSettlements.AsNoTracking().AsSplitQuery()
            .Include(item => item.Project).Include(item => item.Contract).Include(item => item.LegalEntity).Include(item => item.BusinessPartner)
            .Include(item => item.Adjustments).Include(item => item.Deductions)
            .Include(item => item.InvoiceAllocations).ThenInclude(item => item.Invoice)
            .Include(item => item.CashAllocations).ThenInclude(item => item.CashEntry)
            .Where(item => item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value) && item.Direction == direction)
            .OrderBy(item => item.Project!.ProjectNumber).ThenBy(item => item.BusinessDate).ToListAsync(token);
        return items.Select(item =>
        {
            var adjustments = item.Adjustments.Where(value => value.Status == LedgerRecordStatus.Active).ToArray();
            var deductions = item.Deductions.Where(value => value.Status == LedgerRecordStatus.Active).ToArray();
            var gross = item.OriginalAmount + adjustments.Sum(value => value.AmountDelta);
            var baseInvoice = item.OriginalInvoiceAmount + adjustments.Sum(value => value.InvoiceAmountDelta);
            var invoiced = item.InvoiceAllocations.Where(value => value.Invoice.Status == LedgerRecordStatus.Active).Sum(value => value.Amount);
            var cash = item.CashAllocations.Where(value => value.CashEntry.Status == LedgerRecordStatus.Active).Sum(value => value.CashEntry.IsReversal ? -value.Amount : value.Amount);
            var metrics = CentralLedgerCalculator.Calculate(new CentralLedgerCalculationInput(gross, deductions.Sum(value => value.Amount), deductions.Where(value => value.ReduceInvoiceAmount).Sum(value => value.Amount), baseInvoice, invoiced, cash));
            return Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["project_number"] = item.Project!.ProjectNumber, ["contract_number"] = item.Contract?.ContractNumber, ["legal_entity_code"] = item.LegalEntity.Code, ["partner_number"] = item.BusinessPartner?.PartnerNumber,
                ["source_type"] = item.SourceType.ToString(), ["settlement_state"] = item.SettlementState.ToString(), ["entry_date"] = item.BusinessDate,
                ["original_amount"] = item.OriginalAmount, ["actual_amount"] = metrics.ActualAmount, ["original_invoice_amount"] = item.OriginalInvoiceAmount,
                ["current_invoice_amount"] = metrics.ShouldInvoiceAmount, ["invoice_allocation_status"] = AllocationStatus(metrics.InvoicedAmount, metrics.ShouldInvoiceAmount).ToString(),
                ["cash_allocation_status"] = AllocationStatus(metrics.CashAmount, metrics.ActualAmount).ToString(), ["amount"] = metrics.ActualAmount, ["description"] = item.Notes,
                ["is_voided"] = item.Status == LedgerRecordStatus.Voided, ["_system_id"] = item.Id.ToString(), ["_project_system_id"] = item.ProjectId?.ToString(),
                ["_contract_system_id"] = item.ContractId?.ToString(), ["_concurrency_stamp"] = item.ConcurrencyStamp.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
            });
        });
    }

    private async Task<IEnumerable<IReadOnlyList<object?>>> CashRowsAsync(IReadOnlyList<Guid> projectIds, LedgerDirection direction, IReadOnlyList<ProjectWorkbookFieldDefinition> fields, CancellationToken token)
    {
        var items = await db.FinanceCashEntries.AsNoTracking().AsSplitQuery()
            .Include(item => item.Project).Include(item => item.Contract).Include(item => item.LegalEntity).Include(item => item.BusinessPartner).Include(item => item.Account)
            .Include(item => item.Allocations).ThenInclude(item => item.Project)
            .Include(item => item.Allocations).ThenInclude(item => item.Contract)
            .Where(item => item.Direction == direction && !item.IsReversal &&
                ((item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value)) || item.Allocations.Any(allocation => allocation.ProjectId.HasValue && projectIds.Contains(allocation.ProjectId.Value))))
            .OrderBy(item => item.Project!.ProjectNumber).ThenBy(item => item.BusinessDate).ThenBy(item => item.Id).ToListAsync(token);
        return items.Select(item =>
        {
            var projectId = item.ProjectId ?? item.Allocations.Where(allocation => allocation.ProjectId.HasValue && projectIds.Contains(allocation.ProjectId.Value)).Select(allocation => allocation.ProjectId).FirstOrDefault();
            var project = item.Project ?? item.Allocations.First(allocation => allocation.ProjectId == projectId).Project!;
            var matchingAllocations = item.Allocations.Where(allocation => allocation.ProjectId == projectId).ToArray();
            var contractId = item.ProjectId.HasValue ? item.ContractId : matchingAllocations.Select(allocation => allocation.ContractId).Distinct().Count() == 1 ? matchingAllocations[0].ContractId : null;
            var contractNumber = item.ProjectId.HasValue ? item.Contract?.ContractNumber : matchingAllocations.FirstOrDefault(allocation => allocation.ContractId == contractId)?.Contract?.ContractNumber;
            var amount = item.ProjectId.HasValue ? item.Amount : matchingAllocations.Sum(allocation => allocation.Amount);
            var payableId = direction == LedgerDirection.Payable && matchingAllocations.Length == 1 ? matchingAllocations[0].SettlementId.ToString() : null;
            return Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["project_number"] = project.ProjectNumber, ["contract_number"] = contractNumber, ["legal_entity_code"] = item.LegalEntity.Code, ["partner_number"] = item.BusinessPartner?.PartnerNumber,
                ["payable_id"] = payableId, [direction == LedgerDirection.Receivable ? "collection_date" : "payment_date"] = item.BusinessDate,
                ["account_name"] = item.Account?.AccountName, ["account_id"] = item.AccountId?.ToString(), ["amount"] = amount, ["payment_method"] = item.PaymentMethod, ["notes"] = item.Notes,
                ["_system_id"] = item.Id.ToString(), ["_project_system_id"] = projectId?.ToString(), ["_contract_system_id"] = contractId?.ToString(), ["_concurrency_stamp"] = item.ConcurrencyStamp.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
            });
        });
    }

    private async Task<IEnumerable<IReadOnlyList<object?>>> CentralInvoiceRowsAsync(IReadOnlyList<Guid> projectIds, IReadOnlyList<ProjectWorkbookFieldDefinition> fields, CancellationToken token)
    {
        var items = await db.FinanceInvoices.AsNoTracking().AsSplitQuery()
            .Include(item => item.Project).Include(item => item.Contract).Include(item => item.LegalEntity).Include(item => item.BusinessPartner)
            .Include(item => item.Allocations).ThenInclude(item => item.Project)
            .Include(item => item.Allocations).ThenInclude(item => item.Contract)
            .Where(item => item.Direction == LedgerDirection.Receivable &&
                ((item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value)) || item.Allocations.Any(allocation => allocation.ProjectId.HasValue && projectIds.Contains(allocation.ProjectId.Value))))
            .OrderBy(item => item.Project!.ProjectNumber).ThenBy(item => item.InvoiceDate).ThenBy(item => item.Id).ToListAsync(token);
        return items.Select(item =>
        {
            var projectId = item.ProjectId ?? item.Allocations.Where(allocation => allocation.ProjectId.HasValue && projectIds.Contains(allocation.ProjectId.Value)).Select(allocation => allocation.ProjectId).FirstOrDefault();
            var project = item.Project ?? item.Allocations.First(allocation => allocation.ProjectId == projectId).Project!;
            var matchingAllocations = item.Allocations.Where(allocation => allocation.ProjectId == projectId).ToArray();
            var contractId = item.ProjectId.HasValue ? item.ContractId : matchingAllocations.Select(allocation => allocation.ContractId).Distinct().Count() == 1 ? matchingAllocations[0].ContractId : null;
            var contractNumber = item.ProjectId.HasValue ? item.Contract?.ContractNumber : matchingAllocations.FirstOrDefault(allocation => allocation.ContractId == contractId)?.Contract?.ContractNumber;
            var gross = item.ProjectId.HasValue ? item.Amount : matchingAllocations.Sum(allocation => allocation.Amount);
            var ratio = item.Amount == 0m ? 0m : gross / item.Amount;
            return Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["project_number"] = project.ProjectNumber, ["contract_number"] = contractNumber, ["legal_entity_code"] = item.LegalEntity.Code, ["partner_number"] = item.BusinessPartner?.PartnerNumber,
                ["invoice_number"] = item.InvoiceNumber, ["invoice_date"] = item.InvoiceDate, ["invoice_type"] = item.InvoiceType, ["tax_rate"] = item.TaxRate,
                ["net_amount"] = (item.NetAmount ?? 0m) * ratio, ["tax_amount"] = (item.TaxAmount ?? 0m) * ratio, ["gross_amount"] = gross,
                ["status"] = item.Status.ToString(), ["_system_id"] = item.Id.ToString(), ["_project_system_id"] = projectId?.ToString(),
                ["_contract_system_id"] = contractId?.ToString(), ["_concurrency_stamp"] = item.ConcurrencyStamp.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
            });
        });
    }

    private async Task<IEnumerable<IReadOnlyList<object?>>> DeductionRowsAsync(IReadOnlyList<Guid> projectIds, IReadOnlyList<ProjectWorkbookFieldDefinition> fields, CancellationToken token)
    {
        var items = await db.FinanceDeductions.AsNoTracking().Include(item => item.Settlement).ThenInclude(item => item.Project).Include(item => item.Settlement).ThenInclude(item => item.Contract)
            .Include(item => item.Settlement).ThenInclude(item => item.LegalEntity).Include(item => item.Settlement).ThenInclude(item => item.BusinessPartner)
            .Where(item => item.Settlement.ProjectId.HasValue && projectIds.Contains(item.Settlement.ProjectId.Value))
            .OrderBy(item => item.Settlement.Project!.ProjectNumber).ThenBy(item => item.BusinessDate).ToListAsync(token);
        return items.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["project_number"] = item.Settlement.Project!.ProjectNumber, ["contract_number"] = item.Settlement.Contract?.ContractNumber, ["legal_entity_code"] = item.Settlement.LegalEntity.Code,
            ["partner_number"] = item.Settlement.BusinessPartner?.PartnerNumber, ["settlement_id"] = item.SettlementId.ToString(), ["deduction_date"] = item.BusinessDate,
            ["amount"] = item.Amount, ["reduce_invoice_amount"] = item.ReduceInvoiceAmount, ["reason"] = item.Reason, ["status"] = item.Status.ToString(), ["_system_id"] = item.Id.ToString(),
            ["_project_system_id"] = item.Settlement.ProjectId?.ToString(), ["_contract_system_id"] = item.Settlement.ContractId?.ToString(), ["_concurrency_stamp"] = item.ConcurrencyStamp.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
        }));
    }

    private static LedgerAllocationStatus AllocationStatus(decimal allocated, decimal target) => target <= 0m || allocated >= target ? LedgerAllocationStatus.FullyAllocated : allocated <= 0m ? LedgerAllocationStatus.Unallocated : LedgerAllocationStatus.PartiallyAllocated;

    private static IEnumerable<IReadOnlyList<object?>> ReceivableRows(List<ReceivableEntry> items, IReadOnlyList<ProjectWorkbookFieldDefinition> fields) =>
        items.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["project_number"] = item.Project.ProjectNumber, ["contract_number"] = item.Contract?.ContractNumber, ["legal_entity_code"] = item.LegalEntity.Code, ["partner_number"] = item.BusinessPartner?.PartnerNumber, ["source_type"] = item.SourceType.ToString(), ["entry_date"] = item.EntryDate, ["due_date"] = item.DueDate, ["amount"] = item.Amount, ["description"] = item.Description, ["is_voided"] = item.IsVoided,
            ["_system_id"] = item.Id.ToString(), ["_project_system_id"] = item.ProjectId.ToString(), ["_contract_system_id"] = item.ContractId?.ToString(), ["_concurrency_stamp"] = item.ConcurrencyStamp.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
        }));

    private static IEnumerable<IReadOnlyList<object?>> CollectionRows(List<CollectionEntry> items, IReadOnlyList<ProjectWorkbookFieldDefinition> fields) =>
        items.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["project_number"] = item.Project.ProjectNumber, ["contract_number"] = item.Contract?.ContractNumber, ["legal_entity_code"] = item.LegalEntity.Code, ["partner_number"] = item.BusinessPartner?.PartnerNumber, ["receivable_id"] = item.ReceivableEntryId?.ToString(), ["collection_date"] = item.CollectionDate, ["account_name"] = item.Account.AccountName, ["account_id"] = item.AccountId.ToString(), ["amount"] = item.Amount, ["payment_method"] = item.PaymentMethod.ToString(), ["notes"] = item.Notes,
            ["_system_id"] = item.Id.ToString(), ["_project_system_id"] = item.ProjectId.ToString(), ["_contract_system_id"] = item.ContractId?.ToString(), ["_concurrency_stamp"] = item.ConcurrencyStamp.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
        }));

    private static IEnumerable<IReadOnlyList<object?>> PayableRows(List<PayableEntry> items, IReadOnlyList<ProjectWorkbookFieldDefinition> fields) =>
        items.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["project_number"] = item.Project.ProjectNumber, ["contract_number"] = item.Contract?.ContractNumber, ["legal_entity_code"] = item.LegalEntity.Code, ["partner_number"] = item.BusinessPartner.PartnerNumber, ["source_type"] = item.SourceType.ToString(), ["entry_date"] = item.EntryDate, ["due_date"] = item.DueDate, ["amount"] = item.Amount, ["description"] = item.Description, ["is_voided"] = item.IsVoided,
            ["_system_id"] = item.Id.ToString(), ["_project_system_id"] = item.ProjectId.ToString(), ["_contract_system_id"] = item.ContractId?.ToString(), ["_concurrency_stamp"] = item.ConcurrencyStamp.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
        }));

    private static IEnumerable<IReadOnlyList<object?>> PaymentRows(List<PaymentEntry> items, IReadOnlyList<ProjectWorkbookFieldDefinition> fields) =>
        items.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["project_number"] = item.Project.ProjectNumber, ["contract_number"] = item.Contract?.ContractNumber, ["legal_entity_code"] = item.LegalEntity.Code, ["partner_number"] = item.BusinessPartner.PartnerNumber, ["payable_id"] = item.PayableEntryId?.ToString(), ["payment_date"] = item.PaymentDate, ["account_name"] = item.Account.AccountName, ["account_id"] = item.AccountId.ToString(), ["amount"] = item.Amount, ["payment_method"] = item.PaymentMethod.ToString(), ["notes"] = item.Notes,
            ["_system_id"] = item.Id.ToString(), ["_project_system_id"] = item.ProjectId.ToString(), ["_contract_system_id"] = item.ContractId?.ToString(), ["_concurrency_stamp"] = item.ConcurrencyStamp.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
        }));

    private static IEnumerable<IReadOnlyList<object?>> InvoiceRows(List<InvoiceEntry> items, IReadOnlyList<ProjectWorkbookFieldDefinition> fields) =>
        items.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["project_number"] = item.Project.ProjectNumber, ["contract_number"] = item.Contract?.ContractNumber, ["legal_entity_code"] = item.LegalEntity.Code, ["partner_number"] = item.BusinessPartner?.PartnerNumber, ["direction"] = item.Direction.ToString(), ["invoice_number"] = item.InvoiceNumber, ["invoice_date"] = item.InvoiceDate, ["invoice_type"] = item.InvoiceType, ["tax_rate"] = item.TaxRate, ["net_amount"] = item.NetAmount, ["tax_amount"] = item.TaxAmount, ["gross_amount"] = item.GrossAmount, ["status"] = item.Status.ToString(),
            ["_system_id"] = item.Id.ToString(), ["_project_system_id"] = item.ProjectId.ToString(), ["_contract_system_id"] = item.ContractId?.ToString(), ["_concurrency_stamp"] = item.ConcurrencyStamp.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
        }));

    private async Task<List<Attachment>> AttachmentQuery(IReadOnlyList<Guid> projectIds, CancellationToken cancellationToken) =>
        await db.Attachments.AsNoTracking().Include(item => item.Project).Include(item => item.Contract).ThenInclude(item => item!.Project).Include(item => item.StageResult).ThenInclude(item => item!.Project).Include(item => item.StageResult).ThenInclude(item => item!.Contract)
            .Where(item => !item.IsDeleted && ((item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value)) || (item.ContractId.HasValue && item.Contract != null && projectIds.Contains(item.Contract.ProjectId)) || (item.StageResultId.HasValue && item.StageResult != null && projectIds.Contains(item.StageResult.ProjectId))))
            .OrderBy(item => item.OriginalFileName).ToListAsync(cancellationToken);

    private static IEnumerable<IReadOnlyList<object?>> AttachmentRows(List<Attachment> items, IReadOnlyList<ProjectWorkbookFieldDefinition> fields, IReadOnlyDictionary<Guid, string> hashes) =>
        items.Select(item => Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["project_number"] = item.Project?.ProjectNumber ?? item.Contract?.Project?.ProjectNumber ?? item.StageResult?.Project?.ProjectNumber,
            ["contract_number"] = item.Contract?.ContractNumber, ["stage_result_id"] = item.StageResultId?.ToString(), ["relation_type"] = item.ProjectId.HasValue ? "项目" : item.ContractId.HasValue ? "合同" : "阶段成果", ["original_file_name"] = item.OriginalFileName, ["content_type"] = item.ContentType, ["category"] = item.Category.ToString(), ["description"] = item.Description, ["size_bytes"] = item.SizeBytes, ["uploaded_at"] = item.UploadedAt, ["relative_path"] = $"attachments/{item.Id:N}/{Path.GetFileName(item.OriginalFileName)}", ["sha256"] = hashes.GetValueOrDefault(item.Id),
            ["_system_id"] = item.Id.ToString(), ["_project_system_id"] = item.ProjectId?.ToString() ?? item.Contract?.ProjectId.ToString() ?? item.StageResult?.ProjectId.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
        }));

    private async Task<Dictionary<Guid, string>> ComputeAttachmentHashesAsync(List<Attachment> attachments, CancellationToken cancellationToken)
    {
        if (fileStore is null) throw new InvalidOperationException("导出附件需要文件存储。");
        var hashes = new Dictionary<Guid, string>();
        foreach (var attachment in attachments)
        {
            await using var stream = await fileStore.OpenReadAsync(attachment.StoredName, cancellationToken);
            hashes[attachment.Id] = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        }
        return hashes;
    }

    private async Task<IReadOnlyList<IReadOnlyList<object?>>> SummaryRowsAsync(List<Project> projects, IReadOnlyList<ProjectWorkbookFieldDefinition> fields, DateOnly? cutoffDate, CancellationToken cancellationToken)
    {
        var rows = new List<IReadOnlyList<object?>>(projects.Count);
        foreach (var project in projects)
        {
            var summary = ProjectSummaryService.Calculate(project);
            var finance = await financeService.GetSummaryAsync(new FinanceSummaryFilter(project.Id, CutoffDate: cutoffDate), cancellationToken);
            rows.Add(Project(fields, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["project_number"] = project.ProjectNumber, ["project_name"] = project.Name, ["contract_amount"] = summary.ContractAmount, ["estimated_amount"] = summary.EstimatedAmount, ["settled_amount"] = summary.SettledAmount, ["current_project_amount"] = summary.CurrentAmount,
                ["receivable_amount"] = finance.ReceivableAmount, ["collected_amount"] = finance.CollectedAmount, ["uncollected_amount"] = finance.UncollectedAmount, ["payable_amount"] = finance.PayableAmount, ["paid_amount"] = finance.PaidAmount, ["unpaid_amount"] = finance.UnpaidAmount, ["output_invoice_amount"] = finance.OutputInvoiceAmount, ["uninvoiced_amount"] = finance.UninvoicedAmount, ["_dataset_version"] = ProjectWorkbookVersions.Dataset
            }));
        }
        return rows;
    }

    private static object?[] Project(IReadOnlyList<ProjectWorkbookFieldDefinition> fields, IReadOnlyDictionary<string, object?> values) =>
        fields.Select(field => values.GetValueOrDefault(field.Key)).ToArray();
}
