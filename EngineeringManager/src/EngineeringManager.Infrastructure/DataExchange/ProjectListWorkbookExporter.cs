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
        new("general_contractor_contact", "总包联系人 / 电话"),
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
        new("collection_rate", "收款率"),
        new("collection_receivable_amount", "应收金额"),
        new("collection_collected_amount", "已收金额"),
        new("collection_uncollected_amount", "未收金额"),
        new("payment_rate", "付款率"),
        new("payment_payable_amount", "应付金额"),
        new("payment_paid_amount", "已付金额"),
        new("payment_unpaid_amount", "未付金额"),
        new("invoice_rate", "开票率"),
        new("invoice_invoiced_amount", "已开票金额"),
        new("invoice_uninvoiced_amount", "未开票金额"),
        new("notes", "备注摘要")
    ];

    private static readonly Dictionary<string, PageColumn> ColumnMap =
        Columns.ToDictionary(item => item.Key, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, XlsxColumnOptions> ColumnLayouts =
        new Dictionary<string, XlsxColumnOptions>(StringComparer.Ordinal)
        {
            ["serial_number"] = new(Width: 5.28571428571429, HorizontalAlignment: XlsxHorizontalAlignment.Center, FontName: "Calibri", FontSize: 10, TotalFontName: "Calibri", TotalFontSize: 10, TotalHorizontalAlignment: XlsxHorizontalAlignment.Center),
            ["project_number"] = new(Width: 18, HorizontalAlignment: XlsxHorizontalAlignment.General, WrapText: true, FontName: "宋体", FontSize: 9, TotalFontName: "Calibri", TotalFontSize: 10, TotalWrapText: true),
            ["project_name"] = new(Width: 21.847619047619, WrapText: true, HorizontalAlignment: XlsxHorizontalAlignment.General, FontName: "宋体", FontSize: 9, TotalFontName: "宋体", TotalFontSize: 10, TotalWrapText: true, TotalHorizontalAlignment: XlsxHorizontalAlignment.Center),
            ["stage"] = new(Width: 14, HorizontalAlignment: XlsxHorizontalAlignment.General, FontName: "宋体", FontSize: 9, TotalFontName: "Calibri", TotalFontSize: 10, TotalWrapText: true),
            ["contract_signing_status"] = new(Width: 16, WrapText: true, HorizontalAlignment: XlsxHorizontalAlignment.General, FontName: "宋体", FontSize: 9, TotalFontName: "Calibri", TotalFontSize: 10, TotalWrapText: true),
            ["affiliation_type"] = new(Width: 18, WrapText: true, HorizontalAlignment: XlsxHorizontalAlignment.General, FontName: "宋体", FontSize: 9, TotalFontName: "Calibri", TotalFontSize: 10, TotalWrapText: true),
            ["parent_project"] = new(Width: 24, WrapText: true, HorizontalAlignment: XlsxHorizontalAlignment.General, FontName: "宋体", FontSize: 9, TotalFontName: "Calibri", TotalFontSize: 10, TotalWrapText: true),
            ["general_contractor"] = new(Width: 13.2190476190476, WrapText: true, HorizontalAlignment: XlsxHorizontalAlignment.General, FontName: "宋体", FontSize: 8, TotalFontName: "Calibri", TotalFontSize: 10, TotalWrapText: true),
            ["general_contractor_contact"] = new(Width: 17.7142857142857, WrapText: true, HorizontalAlignment: XlsxHorizontalAlignment.General, FontName: "宋体", FontSize: 9, TotalFontName: "Calibri", TotalFontSize: 10, TotalWrapText: true),
            ["responsible_user"] = new(Width: 22, WrapText: true, HorizontalAlignment: XlsxHorizontalAlignment.General, FontName: "宋体", FontSize: 9, TotalFontName: "Calibri", TotalFontSize: 10, TotalWrapText: true),
            ["department"] = new(Width: 16, HorizontalAlignment: XlsxHorizontalAlignment.General, FontName: "宋体", FontSize: 9, TotalFontName: "Calibri", TotalFontSize: 10, TotalWrapText: true),
            ["branch"] = new(Width: 16, HorizontalAlignment: XlsxHorizontalAlignment.General, FontName: "宋体", FontSize: 9, TotalFontName: "Calibri", TotalFontSize: 10, TotalWrapText: true),
            ["legal_entities"] = new(Width: 22, WrapText: true, HorizontalAlignment: XlsxHorizontalAlignment.General, FontName: "宋体", FontSize: 9, TotalFontName: "Calibri", TotalFontSize: 10, TotalWrapText: true),
            ["actual_start_date"] = new(Width: 14, HorizontalAlignment: XlsxHorizontalAlignment.General, FontName: "宋体", FontSize: 9, TotalFontName: "Calibri", TotalFontSize: 10, TotalWrapText: true),
            ["actual_completion_date"] = new(Width: 14, HorizontalAlignment: XlsxHorizontalAlignment.General, FontName: "宋体", FontSize: 9, TotalFontName: "Calibri", TotalFontSize: 10, TotalWrapText: true),
            ["contract_amount"] = new(Width: 10, HorizontalAlignment: XlsxHorizontalAlignment.General, NumberFormat: "0_ ", FontName: "Calibri", FontSize: 10, TotalFontName: "Calibri", TotalFontSize: 10),
            ["estimated_amount"] = new(Width: 10, HorizontalAlignment: XlsxHorizontalAlignment.General, NumberFormat: "0_ ", FontName: "Calibri", FontSize: 10, TotalFontName: "Calibri", TotalFontSize: 10),
            ["settled_amount"] = new(Width: 10, HorizontalAlignment: XlsxHorizontalAlignment.General, NumberFormat: "0_ ", FontName: "Calibri", FontSize: 10, TotalFontName: "Calibri", TotalFontSize: 10),
            ["current_project_amount"] = new(Width: 10.7142857142857, HorizontalAlignment: XlsxHorizontalAlignment.General, NumberFormat: "0_ ", FontName: "Calibri", FontSize: 10, TotalFontName: "Calibri", TotalFontSize: 10),
            ["settlement_status"] = new(Width: 14, HorizontalAlignment: XlsxHorizontalAlignment.General, FontName: "宋体", FontSize: 9, TotalFontName: "Calibri", TotalFontSize: 10, TotalWrapText: true),
            ["contract_count"] = new(Width: 10, HorizontalAlignment: XlsxHorizontalAlignment.General, NumberFormat: "0_ ", FontName: "Calibri", FontSize: 10, TotalFontName: "Calibri", TotalFontSize: 10),
            ["line_item_count"] = new(Width: 10, HorizontalAlignment: XlsxHorizontalAlignment.General, NumberFormat: "0_ ", FontName: "Calibri", FontSize: 10, TotalFontName: "Calibri", TotalFontSize: 10),
            ["collection_rate"] = new(Width: 6.71428571428571, HorizontalAlignment: XlsxHorizontalAlignment.General, NumberFormat: "0%", FontName: "Calibri", FontSize: 10, TotalFontName: "Calibri", TotalFontSize: 10),
            ["collection_receivable_amount"] = new(Width: 10, HorizontalAlignment: XlsxHorizontalAlignment.General, NumberFormat: "0_ ", FontName: "Calibri", FontSize: 10, TotalFontName: "Calibri", TotalFontSize: 10),
            ["collection_collected_amount"] = new(Width: 10, HorizontalAlignment: XlsxHorizontalAlignment.General, NumberFormat: "0_ ", FontName: "Calibri", FontSize: 10, TotalFontName: "Calibri", TotalFontSize: 10),
            ["collection_uncollected_amount"] = new(Width: 10, HorizontalAlignment: XlsxHorizontalAlignment.General, NumberFormat: "0_ ", FontName: "Calibri", FontSize: 10, TotalFontName: "Calibri", TotalFontSize: 10),
            ["payment_rate"] = new(Width: 12, HorizontalAlignment: XlsxHorizontalAlignment.General, NumberFormat: "0%", FontName: "Calibri", FontSize: 10, TotalFontName: "Calibri", TotalFontSize: 10),
            ["payment_payable_amount"] = new(Width: 14, HorizontalAlignment: XlsxHorizontalAlignment.General, NumberFormat: "0_ ", FontName: "Calibri", FontSize: 10, TotalFontName: "Calibri", TotalFontSize: 10),
            ["payment_paid_amount"] = new(Width: 14, HorizontalAlignment: XlsxHorizontalAlignment.General, NumberFormat: "0_ ", FontName: "Calibri", FontSize: 10, TotalFontName: "Calibri", TotalFontSize: 10),
            ["payment_unpaid_amount"] = new(Width: 14, HorizontalAlignment: XlsxHorizontalAlignment.General, NumberFormat: "0_ ", FontName: "Calibri", FontSize: 10, TotalFontName: "Calibri", TotalFontSize: 10),
            ["invoice_rate"] = new(Width: 12, HorizontalAlignment: XlsxHorizontalAlignment.General, NumberFormat: "0%", FontName: "Calibri", FontSize: 10, TotalFontName: "Calibri", TotalFontSize: 10),
            ["invoice_invoiced_amount"] = new(Width: 10.7142857142857, HorizontalAlignment: XlsxHorizontalAlignment.General, NumberFormat: "0_ ", FontName: "Calibri", FontSize: 10, TotalFontName: "Calibri", TotalFontSize: 10),
            ["invoice_uninvoiced_amount"] = new(Width: 10.7142857142857, HorizontalAlignment: XlsxHorizontalAlignment.General, NumberFormat: "0_ ", FontName: "Calibri", FontSize: 10, TotalFontName: "Calibri", TotalFontSize: 10),
            ["notes"] = new(Width: 32.2857142857143, WrapText: true, HorizontalAlignment: XlsxHorizontalAlignment.General, FontName: "宋体", FontSize: 10, TotalFontName: "Calibri", TotalFontSize: 10, TotalWrapText: true)
        };

    public static XlsxWorksheetOptions CreateWorksheetOptions(
        IReadOnlyList<string> columnKeys,
        IReadOnlyList<object?>? totalRow = null) =>
        new(
            ColumnOptions: columnKeys
                .Select((key, index) => new
                {
                    Index = index,
                    Options = ColumnLayouts.GetValueOrDefault(key)
                        ?? new XlsxColumnOptions(Width: 18, WrapText: true, HorizontalAlignment: XlsxHorizontalAlignment.Left)
                })
                .ToDictionary(item => item.Index, item => item.Options),
            FreezeTopRow: true,
            AutoFilter: true,
            AutoFitWrappedRows: false,
            HideGridLines: true,
            TotalRow: totalRow,
            HeaderRowHeight: 30,
            BodyRowHeight: 25,
            TotalRowHeight: 25,
            DefaultColumnWidth: 10.2857142857143,
            DefaultRowHeight: 15,
            ZoomScale: 115,
            PageSetup: new XlsxPageSetupOptions(
                PaperSize: 9,
                Landscape: true,
                HorizontalDpi: 600,
                FitToWidth: 1,
                FitToHeight: 0,
                FitToPage: true),
            PageMargins: new XlsxPageMarginsOptions(
                Left: 0.0388888888888889,
                Right: 0.0388888888888889,
                Top: 0.196527777777778,
                Bottom: 0.196527777777778,
                Header: 0.5,
                Footer: 0.5),
            RepeatHeaderRowOnPrint: true,
            HeaderBorderColor: "FF8091A3",
            BodyBorderColor: "FF000000",
            TotalBorderColor: "FF000000");

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
        var totalRow = BuildTotalRow(columns, rows);

        return new ProjectListWorkbookData(
            columns.Select(item => item.Header).ToArray(),
            rows,
            selectedIds,
            columns.Select(item => item.Key).ToArray(),
            totalRow);
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

    private static object?[] BuildTotalRow(
        IReadOnlyList<PageColumn> columns,
        object?[][] rows)
    {
        var labelIndex = columns
            .Select((column, index) => new { column.Key, index })
            .FirstOrDefault(item => item.Key == "project_name")?.index
            ?? columns
                .Select((column, index) => new { column.Key, index })
                .FirstOrDefault(item => !TotalColumns.Contains(item.Key) && !RateColumns.Contains(item.Key))?.index
            ?? 0;
        var firstDataRow = 2;
        var lastDataRow = rows.Length + 1;

        return columns.Select((column, index) =>
        {
            if (index == labelIndex)
            {
                return (object?)"合计";
            }

            if (!TotalColumns.Contains(column.Key))
            {
                return null;
            }

            var total = rows.Sum(row => NumericValue(row.ElementAtOrDefault(index)));
            var columnName = ExcelColumnName(index + 1);
            return new XlsxFormula($"SUM({columnName}{firstDataRow}:{columnName}{lastDataRow})", total);
        }).ToArray();
    }

    private static decimal NumericValue(object? value) => value switch
    {
        byte number => number,
        sbyte number => number,
        short number => number,
        ushort number => number,
        int number => number,
        uint number => number,
        long number => number,
        ulong number => number,
        float number => (decimal)number,
        double number => (decimal)number,
        decimal number => number,
        _ => 0m
    };

    private static string ExcelColumnName(int columnNumber)
    {
        var name = string.Empty;
        while (columnNumber > 0)
        {
            columnNumber--;
            name = (char)('A' + (columnNumber % 26)) + name;
            columnNumber /= 26;
        }

        return name;
    }

    private static readonly HashSet<string> RateColumns =
    [
        "collection_rate",
        "payment_rate",
        "invoice_rate"
    ];

    private static readonly HashSet<string> TotalColumns =
    [
        "contract_amount",
        "estimated_amount",
        "settled_amount",
        "current_project_amount",
        "contract_count",
        "line_item_count",
        "collection_receivable_amount",
        "collection_collected_amount",
        "collection_uncollected_amount",
        "payment_payable_amount",
        "payment_paid_amount",
        "payment_unpaid_amount",
        "invoice_invoiced_amount",
        "invoice_uninvoiced_amount"
    ];

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
            ["general_contractor_contact"] = ProjectContactDisplay.Format(project.GeneralContractorContact, project.GeneralContractorPhone),
            ["responsible_user"] = project.ResponsibleEmployeeNames is { Count: > 0 }
                ? string.Join("、", project.ResponsibleEmployeeNames)
                : project.ResponsibleEmployeeName ?? project.ResponsibleUserName ?? "未设置",
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
            ["collection_rate"] = Rate(financeSummary.CollectedAmount, financeSummary.ReceivableAmount),
            ["collection_receivable_amount"] = Amount(financeSummary.ReceivableAmount),
            ["collection_collected_amount"] = Amount(financeSummary.CollectedAmount),
            ["collection_uncollected_amount"] = Amount(financeSummary.UncollectedAmount),
            ["payment_rate"] = Rate(financeSummary.PaidAmount, financeSummary.PayableAmount),
            ["payment_payable_amount"] = Amount(financeSummary.PayableAmount),
            ["payment_paid_amount"] = Amount(financeSummary.PaidAmount),
            ["payment_unpaid_amount"] = Amount(financeSummary.UnpaidAmount),
            ["invoice_rate"] = Rate(financeSummary.OutputInvoiceAmount, summary.CurrentAmount > 0m ? summary.CurrentAmount : summary.ContractAmount),
            ["invoice_invoiced_amount"] = Amount(financeSummary.OutputInvoiceAmount),
            ["invoice_uninvoiced_amount"] = Amount(financeSummary.UninvoicedAmount),
            ["notes"] = Notes(project.Notes)
        };

        return columns.Select(column => values[column.Key]).ToArray();
    }

    private static decimal Amount(decimal value) => value;

    private static string Notes(string? value) => string.IsNullOrWhiteSpace(value)
        ? "—"
        : value.Length > 40 ? value[..40] + "…" : value;

    private static decimal Rate(decimal actual, decimal target)
    {
        return target > 0m ? Math.Clamp(actual / target, 0m, 1m) : 0m;
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
        ContractSigningStatus.NoContract => "不签合同",
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
    IReadOnlyList<Guid> ProjectIds,
    IReadOnlyList<string> ColumnKeys,
    IReadOnlyList<object?> TotalRow);
