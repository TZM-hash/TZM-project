using EngineeringManager.Domain.DataExchange;

namespace EngineeringManager.Web.Pages.DataExchange;

public static class DataExchangeLabels
{
    public static string Dataset(ExportDataset value) => value switch
    {
        ExportDataset.ProjectOverview => "项目经营汇总",
        ExportDataset.Projects => "项目主档",
        ExportDataset.Contracts => "合同与清单",
        ExportDataset.Partners => "合作单位",
        ExportDataset.Employees => "员工档案",
        ExportDataset.Payroll => "工资台账",
        ExportDataset.Collections => "收款记录",
        ExportDataset.Payments => "付款记录",
        ExportDataset.Invoices => "发票记录",
        ExportDataset.Accounts => "资金账户",
        ExportDataset.StageResults => "阶段成果",
        ExportDataset.Companies => "自有公司",
        ExportDataset.CompanyAccounts => "公司账户",
        ExportDataset.CompanyCertificates => "公司证书",
        ExportDataset.Equipment => "设备档案",
        ExportDataset.EquipmentLeases => "设备租赁",
        ExportDataset.EquipmentUsages => "设备使用",
        ExportDataset.EquipmentPeriods => "设备使用期间",
        ExportDataset.EquipmentSettlements => "设备结算",
        ExportDataset.EmployeeCertificates => "员工证书",
        _ => value.ToString()
    };

    public static string Scope(ExportScope value) => value switch
    {
        ExportScope.CurrentView => "当前筛选结果",
        ExportScope.FullAuthorized => "全部授权数据",
        ExportScope.SelectedModules => "勾选的模块",
        _ => value.ToString()
    };

    public static string PackageFormat(ExportPackageFormat value) => value switch
    {
        ExportPackageFormat.Workbook => "单个 Excel 工作簿",
        ExportPackageFormat.Zip => "ZIP 压缩包",
        _ => value.ToString()
    };

    public static string ImportMode(ImportMode value) => value switch
    {
        EngineeringManager.Domain.DataExchange.ImportMode.New => "仅新增",
        EngineeringManager.Domain.DataExchange.ImportMode.Update => "仅更新",
        EngineeringManager.Domain.DataExchange.ImportMode.Mixed => "新增或更新",
        _ => value.ToString()
    };

    public static string TaskStatus(DataExchangeTaskStatus value) => value switch
    {
        DataExchangeTaskStatus.Pending => "排队中",
        DataExchangeTaskStatus.PreviewReady => "预览待确认",
        DataExchangeTaskStatus.Running => "处理中",
        DataExchangeTaskStatus.Completed => "已完成",
        DataExchangeTaskStatus.Failed => "失败",
        _ => value.ToString()
    };
}
