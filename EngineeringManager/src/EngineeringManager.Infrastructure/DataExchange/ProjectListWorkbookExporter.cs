using System.Globalization;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Application.Finance;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Projects;

namespace EngineeringManager.Infrastructure.DataExchange;

public sealed class ProjectListWorkbookExporter(
    IProjectService projectService,
    IFinanceLedgerService financeService)
{
    private static readonly IReadOnlyList<PageColumn> Columns =
    [
        new("serial_number", "序号"),
        new("project_number", "项目编号"),
        new("project_name", "项目名称"),
        new("stage", "阶段"),
        new("contract_signing_status", "合同签订"),
        new("affiliation_type", "合作方式"),
        new("parent_project", "上级项目"),
        new("general_contractor", "总包单位"),
        new("general_contractor_contact", "总包联系人"),
        new("general_contractor_phone", "总包电话"),
        new("responsible_user", "项目负责人"),
        new("department", "部门"),
        new("branch", "分支机构"),
        new("legal_entities", "签约公司"),
        new("actual_start_date", "实际开始日期"),
        new("actual_completion_date", "实际完工日期"),
        new("contract_amount", "合同金额"),
        new("estimated_amount", "预计金额"),
        new("settled_amount", "已结算金额"),
        new("current_project_amount", "当前工程金额"),
        new("settlement_status", "结算状态"),
        new("contract_count", "合同数量"),
        new("line_item_count", "清单项数量"),
        new("collection_progress", "收款率（应 / 已 / 未）"),
        new("payment_progress", "付款率（应 / 已 / 未）"),
        new("invoice_progress", "开票率（已 / 未）"),
        new("notes", "备注摘要")
    ];

    private static readonly Dictionary<string, PageColumn> ColumnMap =
        Columns.ToDictionary(item => item.Key, StringComparer.Ordinal);

    public async Task<ProjectListWorkbookData> BuildAsync(
        ProjectWorkbookScope scope,
        IReadOnlyCollection<string>? requestedColumns,
        CancellationToken cancellationToken)
    {
        var firstPage = await projectService.SearchProjectsAsync(
            scope.Actor,
            scope.Query with { Page = 1, PageSize = 100 },
            cancellationToken);
        var matchingIds = firstPage.MatchingProjectIds;
        var selectedIds = SelectProjectIds(scope, matchingIds);
        if (selectedIds.Length == 0)
        {
            throw new InvalidOperationException("没有可导出的项目。");
        }

        var itemsById = new Dictionary<Guid, ProjectListItemDto>();
        AddItems(itemsById, firstPage.Items);
        for (var page = 2; page <= firstPage.TotalPages; page++)
        {
            var result = await projectService.SearchProjectsAsync(
                scope.Actor,
                scope.Query with { Page = page, PageSize = 100 },
                cancellationToken);
            AddItems(itemsById, result.Items);
        }

        var finance = await financeService.ListProjectSummariesAsync(selectedIds, cancellationToken);
        var financeByProjectId = finance.ToDictionary(item => item.ProjectId, item => item.Summary);
        var columns = NormalizeColumns(requestedColumns);
        var serialByProjectId = matchingIds
            .Select((id, index) => new { id, serialNumber = index + 1 })
            .ToDictionary(item => item.id, item => item.serialNumber);
        var rows = selectedIds
            .Select(id =>
            {
                if (!itemsById.TryGetValue(id, out var item))
                {
                    throw new InvalidOperationException("项目查询结果发生变化，请重新筛选后再导出。");
                }

                return RenderRow(new ProjectListExportItem(item, serialByProjectId[id]), financeByProjectId.GetValueOrDefault(id), columns);
            })
            .ToArray();

        return new ProjectListWorkbookData(
            columns.Select(item => item.Header).ToArray(),
            rows,
            selectedIds);
    }

    private static Guid[] SelectProjectIds(ProjectWorkbookScope scope, IReadOnlyList<Guid> matchingIds)
    {
        if (scope.SelectAllMatching)
        {
            return matchingIds.ToArray();
        }

        var selected = (scope.SelectedProjectIds ?? []).ToHashSet();
        return matchingIds.Where(selected.Contains).ToArray();
    }

    private static void AddItems(Dictionary<Guid, ProjectListItemDto> target, IReadOnlyList<ProjectListItemDto> items)
    {
        foreach (var item in items)
        {
            target[item.Project.Id] = item;
        }
    }

    private static IReadOnlyList<PageColumn> NormalizeColumns(IReadOnlyCollection<string>? requestedColumns)
    {
        var requested = requestedColumns?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Where(ColumnMap.ContainsKey)
            .Distinct(StringComparer.Ordinal)
            .Select(item => ColumnMap[item])
            .ToArray() ?? [];
        return requested.Length > 0 ? requested : Columns;
    }

    private static object?[] RenderRow(
        ProjectListExportItem item,
        FinanceProjectSummaryDto? finance,
        IReadOnlyList<PageColumn> columns)
    {
        var project = item.Item.Project;
        var summary = item.Item.Summary;
        var financeSummary = finance ?? EmptyFinanceSummary(project.Id);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["serial_number"] = item.SerialNumber,
            ["project_number"] = project.ProjectNumber,
            ["project_name"] = project.Name,
            ["stage"] = StageLabel(project.Stage),
            ["contract_signing_status"] = ContractSigningStatusLabel(project.ContractSigningStatus),
            ["affiliation_type"] = AffiliationLabel(project.AffiliationType),
            ["parent_project"] = project.ParentProjectName ?? "未设置",
            ["general_contractor"] = ProjectGeneralContractors.Display(project.GeneralContractorName),
            ["general_contractor_contact"] = project.GeneralContractorContact ?? "未设置",
            ["general_contractor_phone"] = project.GeneralContractorPhone ?? "未设置",
            ["responsible_user"] = project.ResponsibleUserName ?? "未设置",
            ["department"] = project.DepartmentName ?? "未设置",
            ["branch"] = project.BranchName ?? "未设置",
            ["legal_entities"] = project.LegalEntityNames is { Count: > 0 } ? string.Join("、", project.LegalEntityNames) : "未设置",
            ["actual_start_date"] = project.ActualStartDate.HasValue ? (object)project.ActualStartDate.Value : "未设置",
            ["actual_completion_date"] = project.ActualCompletionDate.HasValue ? (object)project.ActualCompletionDate.Value : "未设置",
            ["contract_amount"] = Amount(summary.ContractAmount),
            ["estimated_amount"] = Amount(summary.EstimatedAmount),
            ["settled_amount"] = Amount(summary.SettledAmount),
            ["current_project_amount"] = Amount(summary.CurrentAmount),
            ["settlement_status"] = SettlementStatusLabel(summary.SettlementStatus),
            ["contract_count"] = summary.ContractCount,
            ["line_item_count"] = summary.LineItemCount,
            ["collection_progress"] = Progress(financeSummary.CollectedAmount, financeSummary.ReceivableAmount, financeSummary.UncollectedAmount),
            ["payment_progress"] = Progress(financeSummary.PaidAmount, financeSummary.PayableAmount, financeSummary.UnpaidAmount),
            ["invoice_progress"] = InvoiceProgress(financeSummary.OutputInvoiceAmount, summary.CurrentAmount > 0m ? summary.CurrentAmount : summary.ContractAmount, financeSummary.UninvoicedAmount),
            ["notes"] = Notes(project.Notes)
        };

        return columns.Select(column => values[column.Key]).ToArray();
    }

    private static string Amount(decimal value) => value.ToString("N2", CultureInfo.CurrentCulture);

    private static string Notes(string? value) => string.IsNullOrWhiteSpace(value)
        ? "—"
        : value.Length > 40 ? value[..40] + "…" : value;

    private static string Progress(decimal actual, decimal target, decimal remaining)
    {
        var rate = target > 0m ? Math.Clamp(actual / target * 100m, 0m, 100m) : 0m;
        return $"{rate.ToString("N0", CultureInfo.CurrentCulture)}%（{Amount(target)} / {Amount(actual)} / {Amount(remaining)}）";
    }

    private static string InvoiceProgress(decimal invoiced, decimal basis, decimal remaining)
    {
        var rate = basis > 0m ? Math.Clamp(invoiced / basis * 100m, 0m, 100m) : 0m;
        return $"{rate.ToString("N0", CultureInfo.CurrentCulture)}%（{Amount(invoiced)} / {Amount(remaining)}）";
    }

    private static string StageLabel(ProjectStage value) => value switch
    {
        ProjectStage.AwaitingMobilization => "待进场",
        ProjectStage.UnderConstruction => "施工中",
        ProjectStage.Suspended => "停工中",
        ProjectStage.CompletedUnsettled => "已完工未结算",
        ProjectStage.PartiallySettled => "部分结算",
        ProjectStage.SettledArchived => "已结算归档",
        _ => "未知阶段"
    };

    private static string ContractSigningStatusLabel(ContractSigningStatus value) => value switch
    {
        ContractSigningStatus.NotSigned => "未签合同",
        ContractSigningStatus.SentForSignature => "合同已寄出",
        ContractSigningStatus.FullySigned => "合同已签完",
        _ => "未知合同状态"
    };

    private static string AffiliationLabel(ProjectAffiliationType value) => value switch
    {
        ProjectAffiliationType.SelfOperated => "自营项目",
        ProjectAffiliationType.ExternalPartyAttachedToUs => "他方挂靠我方",
        ProjectAffiliationType.WeAttachedToExternalParty => "我方挂靠他方",
        _ => "未知合作方式"
    };

    private static string SettlementStatusLabel(ProjectSettlementStatus value) => value switch
    {
        ProjectSettlementStatus.Estimated => "暂估",
        ProjectSettlementStatus.PartiallySettled => "部分结算",
        ProjectSettlementStatus.Settled => "已结算",
        _ => "未知结算状态"
    };

    private static FinanceProjectSummaryDto EmptyFinanceSummary(Guid projectId) =>
        new(projectId, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, false, false);

    private sealed record PageColumn(string Key, string Header);

    private sealed record ProjectListExportItem(ProjectListItemDto Item, int SerialNumber);
}

public sealed record ProjectListWorkbookData(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    IReadOnlyList<Guid> ProjectIds);
