using EngineeringManager.Application.DataExchange;

namespace EngineeringManager.Infrastructure.DataExchange;

public sealed class PersonnelWorkbookExporter : IPersonnelWorkbookService
{
    public static readonly IReadOnlyList<PersonnelWorkbookColumnDefinition> PersonnelWorkbookColumnDefinitions =
    [
        new("person_number", "人员编号"),
        new("name", "姓名"),
        new("phone", "电话"),
        new("personnel_type", "人员类型"),
        new("position", "岗位"),
        new("organization", "公司 / 单位"),
        new("department", "部门"),
        new("project", "项目"),
        new("crew", "班组"),
        new("status", "状态"),
        new("prior_year_carry_forward", "往年结转", true),
        new("current_year_wage_payable", "工资应付", true),
        new("expense_payable", "报销应付", true),
        new("other_payable", "其他应付", true),
        new("adjustment_amount", "调整金额", true),
        new("current_year_new_payable", "本年应发合计", true),
        new("received_amount", "已发合计", true),
        new("current_year_unpaid", "未发合计", true),
        new("current_balance", "当前余额", true),
        new("settlement_progress", "结算进度", true, true),
        new("overpaid_status", "超付状态"),
        new("penalty_amount", "罚款扣减", true)
    ];

    public IReadOnlyList<PersonnelWorkbookColumnDefinition> GetColumns() => PersonnelWorkbookColumnDefinitions;

    public Task<ExportFileResult> ExportAsync(
        PersonnelWorkbookExportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var columns = NormalizeColumns(request.Columns);
        var workbook = new SimpleXlsxWorkbook();
        var rows = request.Rows.Select(row => columns.Select(column => RenderValue(row, column.Key)).ToArray()).ToArray();
        workbook.AddWorksheet(
            "人员清单",
            columns.Select(item => item.Label).ToArray(),
            rows,
            CreateWorksheetOptions(columns));

        var result = new ExportFileResult(
            $"人员清单_{DateTime.Now:yyyyMMddHHmmss}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            workbook.ToArray());
        return Task.FromResult(result);
    }

    private static IReadOnlyList<PersonnelWorkbookColumnDefinition> NormalizeColumns(IReadOnlyCollection<string>? requested)
    {
        var requestedKeys = requested?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToHashSet(StringComparer.Ordinal)
            ?? [];

        var columns = PersonnelWorkbookColumnDefinitions
            .Where(item => requestedKeys.Contains(item.Key))
            .ToArray();
        return columns.Length > 0 ? columns : PersonnelWorkbookColumnDefinitions;
    }

    private static object? RenderValue(PersonnelWorkbookRow row, string key)
    {
        var summary = row.AnnualSummary;
        return key switch
        {
            "person_number" => row.PersonNumber,
            "name" => row.Name,
            "phone" => row.Phone,
            "personnel_type" => row.PersonnelType,
            "position" => row.PositionTitle,
            "organization" => row.OrganizationName,
            "department" => row.DepartmentName,
            "project" => row.ProjectName,
            "crew" => row.CrewName,
            "status" => row.IsActive ? "启用" : "停用",
            "prior_year_carry_forward" => summary?.PriorYearCarryForward,
            "current_year_wage_payable" => summary?.CurrentYearWagePayable,
            "expense_payable" => summary?.ExpensePayable,
            "other_payable" => summary?.OtherPayable,
            "adjustment_amount" => summary?.AdjustmentAmount,
            "current_year_new_payable" => summary?.CurrentYearNewPayable,
            "received_amount" => summary?.ReceivedAmount,
            "current_year_unpaid" => summary is null ? null : summary.CurrentYearNewPayable - summary.ReceivedAmount,
            "current_balance" => summary?.CurrentBalance,
            "settlement_progress" => summary is null ? null : summary.SettlementProgressPercent / 100m,
            "overpaid_status" => summary is null ? null : summary.IsOverpaid ? "超付" : "正常",
            "penalty_amount" => summary is null ? null : row.PenaltyAmount ?? 0m,
            _ => null
        };
    }

    private static XlsxWorksheetOptions CreateWorksheetOptions(IReadOnlyList<PersonnelWorkbookColumnDefinition> columns) =>
        new(
            ColumnOptions: columns
                .Select((column, index) => new
                {
                    Index = index,
                    Options = CreateColumnOptions(column)
                })
                .ToDictionary(item => item.Index, item => item.Options),
            FreezeTopRow: true,
            AutoFilter: true,
            HideGridLines: true,
            HeaderRowHeight: 28,
            BodyRowHeight: 23,
            DefaultColumnWidth: 12,
            DefaultRowHeight: 15,
            ZoomScale: 100,
            PageSetup: new XlsxPageSetupOptions(PaperSize: 9, Landscape: true, FitToWidth: 1, FitToHeight: 0, FitToPage: true),
            RepeatHeaderRowOnPrint: true,
            HeaderBorderColor: "FF8091A3",
            BodyBorderColor: "FFD7DEE8");

    private static XlsxColumnOptions CreateColumnOptions(PersonnelWorkbookColumnDefinition column)
    {
        var width = column.Key switch
        {
            "person_number" => 16,
            "name" => 14,
            "phone" => 16,
            "personnel_type" => 14,
            "position" => 18,
            "organization" => 22,
            "department" => 18,
            "project" => 22,
            "crew" => 18,
            "status" => 10,
            "settlement_progress" => 13,
            "overpaid_status" => 12,
            _ when column.IsNumeric => 15,
            _ => 18
        };

        return new XlsxColumnOptions(
            Width: width,
            WrapText: !column.IsNumeric,
            HorizontalAlignment: column.IsNumeric ? XlsxHorizontalAlignment.Right : XlsxHorizontalAlignment.Left,
            NumberFormat: column.IsPercentage ? "0.00%" : column.IsNumeric ? "0.00" : null,
            FontName: column.IsNumeric ? "Calibri" : "宋体",
            FontSize: 10);
    }
}
