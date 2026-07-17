namespace EngineeringManager.Web.Workbenches;

public static class DataWorkbenchPresets
{
    public static DataWorkbenchViewModel Employees => Create("employees", "employees-table", [("employee_number", "员工编号"), ("name", "姓名"), ("employee_type", "类型"), ("position", "岗位"), ("phone", "电话"), ("assignments", "主归属记录"), ("status", "状态")]);
    public static DataWorkbenchViewModel Payroll => Create("payroll", "payroll-table", [("batch", "批次"), ("period", "区间"), ("type", "类型"), ("payable", "应发"), ("paid", "已发"), ("unpaid", "未发"), ("risk", "风险")]);
    public static DataWorkbenchViewModel EmployeeLedger => Create("employee-ledger", "employee-ledger-table", [("employee", "员工"), ("expense_payable", "报销应付"), ("expense_paid", "报销已付"), ("expense_unpaid", "报销未付"), ("advance", "借支未清"), ("other_payable", "其他应付"), ("other_paid", "其他已付"), ("other_unpaid", "其他未付"), ("risk", "风险")]);
    public static DataWorkbenchViewModel Partners => Create("partners", "partners-table", [("number", "编号"), ("name", "单位名称"), ("roles", "角色"), ("contact", "主要联系人"), ("projects", "参与项目")]);
    public static DataWorkbenchViewModel StageResults => Create("stage-results", "stage-results-table", [("date", "日期"), ("name", "名称"), ("type", "类型"), ("status", "状态"), ("quality", "质量"), ("lines", "工程量项"), ("attachments", "附件")]);
    public static DataWorkbenchViewModel Companies => Create("companies", "companies-table", [("code", "编码"), ("company", "公司"), ("category", "分类"), ("representative", "法人/经营者"), ("status", "状态")]);
    public static DataWorkbenchViewModel Equipment => Create("equipment", "equipment-table", [("number", "编号"), ("name", "名称"), ("model", "型号"), ("ownership", "权属"), ("status", "状态"), ("actions", "操作")]);
    public static DataWorkbenchViewModel Reminders => Create("reminders", "reminders-table", [("severity", "级别"), ("type", "类型"), ("title", "标题"), ("message", "说明"), ("actions", "操作")]);
    public static DataWorkbenchViewModel Users => Create("users", "users-table", [("display_name", "姓名"), ("user_name", "账号"), ("status", "状态"), ("roles", "角色"), ("department", "主部门"), ("companies", "签约公司范围")]);
    public static DataWorkbenchViewModel Organizations => Create("organizations", "organizations-table", [("code", "编码"), ("name", "名称"), ("type", "类型"), ("status", "状态")]);
    public static DataWorkbenchViewModel DataExchange => Create("data-exchange", "data-exchange-table", [("key", "字段键"), ("label", "字段名称"), ("type", "数据类型"), ("default", "默认导出")]);
    public static DataWorkbenchViewModel Backups => Create("backups", "backups-table", [("created", "时间"), ("status", "状态"), ("database", "数据库文件"), ("attachments", "附件文件"), ("error", "错误")]);

    private static DataWorkbenchViewModel Create(string pageKey, string tableId, IReadOnlyList<(string Key, string Label)> columns) =>
        new(pageKey, tableId, columns.Select((item, index) => new DataWorkbenchColumn(item.Key, item.Label, true, index == 0)).ToArray(), [], [], [], CanExport: false, CanSaveViews: false, CanChangePageSize: false);
}
