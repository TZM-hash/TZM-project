using EngineeringManager.Application.DataExchange;
using EngineeringManager.Domain.DataExchange;

namespace EngineeringManager.Infrastructure.DataExchange;

public static class ProjectWorkbookCatalog
{
    public static IReadOnlyList<ProjectWorkbookSheetDefinition> Sheets { get; } =
    [
        Define(ProjectWorkbookSheet.ProjectMaster, "项目主档", true, false, [],
        [
            Field("project_number", "项目编号", ExportFieldDataType.Text, true),
            Field("project_name", "项目名称", ExportFieldDataType.Text, true),
            Field("parent_project", "上级项目", ExportFieldDataType.Text),
            Field("general_contractor", "总包单位", ExportFieldDataType.Text),
            Field("general_contractor_contact", "总包联系人", ExportFieldDataType.Text),
            Field("general_contractor_phone", "总包电话", ExportFieldDataType.Text),
            Field("responsible_user_id", "负责人账号", ExportFieldDataType.Text),
            Field("responsible_user", "项目负责人", ExportFieldDataType.Text, CanImport: false),
            Field("department_id", "部门ID", ExportFieldDataType.Text),
            Field("department", "部门", ExportFieldDataType.Text, CanImport: false),
            Field("branch_id", "分支机构ID", ExportFieldDataType.Text),
            Field("branch", "分支机构", ExportFieldDataType.Text, CanImport: false),
            Field("stage", "项目阶段", ExportFieldDataType.Text, true, Aliases: ["项目状态"]),
            Field("contract_signing_status", "合同状态", ExportFieldDataType.Text, true),
            Field("affiliation_type", "项目合作方式", ExportFieldDataType.Text, true),
            Field("legal_entity_ids", "签约公司ID", ExportFieldDataType.Text),
            Field("legal_entities", "签约公司", ExportFieldDataType.Text, CanImport: false),
            Field("actual_start_date", "实际开工日期", ExportFieldDataType.Date),
            Field("actual_completion_date", "实际完工日期", ExportFieldDataType.Date),
            Field("is_active", "状态", ExportFieldDataType.Boolean),
            Field("notes", "备注", ExportFieldDataType.Text),
            Technical("_system_id", "系统ID"),
            Technical("_project_system_id", "项目系统ID"),
            Technical("_concurrency_stamp", "并发版本"),
            Technical("_dataset_version", "数据集版本")
        ]),
        Define(ProjectWorkbookSheet.ProjectSummary, "项目经营汇总", false, false, [ProjectWorkbookSheet.ProjectMaster],
        [
            Field("project_number", "项目编号", ExportFieldDataType.Text, true, CanImport: false),
            Field("project_name", "项目名称", ExportFieldDataType.Text, CanImport: false),
            Field("contract_amount", "合同金额", ExportFieldDataType.Number, CanImport: false, IsCalculated: true),
            Field("estimated_amount", "预计金额", ExportFieldDataType.Number, CanImport: false, IsCalculated: true),
            Field("settled_amount", "已结算金额", ExportFieldDataType.Number, CanImport: false, IsCalculated: true),
            Field("current_project_amount", "当前工程金额", ExportFieldDataType.Number, CanImport: false, IsCalculated: true),
            Field("receivable_amount", "应收款", ExportFieldDataType.Number, CanImport: false, IsCalculated: true),
            Field("collected_amount", "已收款", ExportFieldDataType.Number, CanImport: false, IsCalculated: true),
            Field("uncollected_amount", "未收款", ExportFieldDataType.Number, CanImport: false, IsCalculated: true),
            Field("payable_amount", "应付款", ExportFieldDataType.Number, CanImport: false, IsCalculated: true),
            Field("paid_amount", "已付款", ExportFieldDataType.Number, CanImport: false, IsCalculated: true),
            Field("unpaid_amount", "未付款", ExportFieldDataType.Number, CanImport: false, IsCalculated: true),
            Field("output_invoice_amount", "已开票", ExportFieldDataType.Number, CanImport: false, IsCalculated: true),
            Field("uninvoiced_amount", "未开票", ExportFieldDataType.Number, CanImport: false, IsCalculated: true),
            Field("_dataset_version", "数据集版本", ExportFieldDataType.Text, CanImport: false, IsCalculated: true)
        ]),
        Define(ProjectWorkbookSheet.Contracts, "合同", true, false, [ProjectWorkbookSheet.ProjectMaster],
        [
            Field("project_number", "项目编号", ExportFieldDataType.Text, true),
            Field("contract_number", "合同编号", ExportFieldDataType.Text, true),
            Field("name", "合同名称", ExportFieldDataType.Text, true),
            Field("contract_type", "合同类型", ExportFieldDataType.Text, true),
            Field("allocation_mode", "分摊方式", ExportFieldDataType.Text, true),
            Field("counterparty_name", "对方单位", ExportFieldDataType.Text),
            Field("signed_date", "签订日期", ExportFieldDataType.Date),
            Field("total_amount", "合同金额", ExportFieldDataType.Number, true),
            Field("is_active", "状态", ExportFieldDataType.Boolean),
            Field("notes", "备注", ExportFieldDataType.Text),
            Technical("_system_id", "系统ID"), Technical("_project_system_id", "项目系统ID"),
            Technical("_concurrency_stamp", "并发版本"), Technical("_dataset_version", "数据集版本")
        ]),
        Define(ProjectWorkbookSheet.QuantityLines, "工程量", true, false, [ProjectWorkbookSheet.Contracts],
        [
            Field("project_number", "项目编号", ExportFieldDataType.Text, true),
            Field("contract_number", "合同编号", ExportFieldDataType.Text, true),
            Field("code", "清单编码", ExportFieldDataType.Text, true),
            Field("name", "清单名称", ExportFieldDataType.Text, true),
            Field("unit", "单位", ExportFieldDataType.Text, true),
            Field("estimated_quantity", "暂估工程量", ExportFieldDataType.Number),
            Field("estimated_unit_price", "暂估单价", ExportFieldDataType.Number),
            Field("settled_quantity", "结算工程量", ExportFieldDataType.Number),
            Field("settled_unit_price", "结算单价", ExportFieldDataType.Number),
            Field("is_settlement_confirmed", "已确认结算", ExportFieldDataType.Boolean),
            Field("notes", "备注", ExportFieldDataType.Text),
            Technical("_system_id", "系统ID"), Technical("_project_system_id", "项目系统ID"),
            Technical("_contract_system_id", "合同系统ID"), Technical("_concurrency_stamp", "并发版本"),
            Technical("_dataset_version", "数据集版本")
        ]),
        Define(ProjectWorkbookSheet.Milestones, "里程碑", true, false, [ProjectWorkbookSheet.ProjectMaster],
        [
            Field("project_number", "项目编号", ExportFieldDataType.Text, true), Field("name", "里程碑名称", ExportFieldDataType.Text, true),
            Field("planned_date", "计划日期", ExportFieldDataType.Date), Field("actual_date", "实际日期", ExportFieldDataType.Date),
            Field("is_completed", "已完成", ExportFieldDataType.Boolean), Field("sort_order", "排序", ExportFieldDataType.Number), Field("notes", "备注", ExportFieldDataType.Text),
            Technical("_system_id", "系统ID"), Technical("_project_system_id", "项目系统ID"), Technical("_dataset_version", "数据集版本")
        ]),
        Define(ProjectWorkbookSheet.Assignments, "项目人员", true, false, [ProjectWorkbookSheet.ProjectMaster],
        [
            Field("project_number", "项目编号", ExportFieldDataType.Text, true), Field("user_id", "人员账号", ExportFieldDataType.Text, true),
            Field("user_name", "人员姓名", ExportFieldDataType.Text, CanImport: false), Field("assignment_type", "人员类型", ExportFieldDataType.Text, true),
            Field("is_active", "状态", ExportFieldDataType.Boolean), Field("notes", "备注", ExportFieldDataType.Text),
            Technical("_system_id", "系统ID"), Technical("_project_system_id", "项目系统ID"), Technical("_dataset_version", "数据集版本")
        ]),
        Define(ProjectWorkbookSheet.Partners, "项目合作单位", true, false, [ProjectWorkbookSheet.ProjectMaster],
        [
            Field("project_number", "项目编号", ExportFieldDataType.Text, true), Field("partner_number", "单位编号", ExportFieldDataType.Text, true),
            Field("partner_name", "单位名称", ExportFieldDataType.Text, CanImport: false), Field("role_type", "业务角色", ExportFieldDataType.Text, true),
            Field("contract_number", "合同编号", ExportFieldDataType.Text), Field("is_primary", "主要单位", ExportFieldDataType.Boolean),
            Field("is_active", "状态", ExportFieldDataType.Boolean), Field("notes", "备注", ExportFieldDataType.Text),
            Technical("_system_id", "系统ID"), Technical("_project_system_id", "项目系统ID"), Technical("_contract_system_id", "合同系统ID"), Technical("_dataset_version", "数据集版本")
        ]),
        Define(ProjectWorkbookSheet.Construction, "施工详情", true, false, [ProjectWorkbookSheet.ProjectMaster],
        [
            Field("project_number", "项目编号", ExportFieldDataType.Text, true), Field("record_type", "记录类型", ExportFieldDataType.Text, true),
            Field("equipment_number", "设备编号", ExportFieldDataType.Text), Field("crew_partner_number", "班组编号", ExportFieldDataType.Text),
            Field("transfer_from_project_number", "调入项目", ExportFieldDataType.Text), Field("transfer_to_project_number", "调出项目", ExportFieldDataType.Text),
            Field("entry_date", "进场日期", ExportFieldDataType.Date), Field("exit_date", "退场日期", ExportFieldDataType.Date),
            Field("stop_days", "停工天数", ExportFieldDataType.Number), Field("is_draft", "待完善", ExportFieldDataType.Boolean),
            Field("show_in_project_overview", "显示在项目总览", ExportFieldDataType.Boolean), Field("notes", "备注", ExportFieldDataType.Text),
            Technical("_system_id", "系统ID"), Technical("_project_system_id", "项目系统ID"), Technical("_dataset_version", "数据集版本")
        ]),
        Define(ProjectWorkbookSheet.StageResults, "阶段成果", true, false, [ProjectWorkbookSheet.Contracts],
        [
            Field("project_number", "项目编号", ExportFieldDataType.Text, true), Field("contract_number", "合同编号", ExportFieldDataType.Text),
            Field("title", "成果标题", ExportFieldDataType.Text, true), Field("result_type", "成果类型", ExportFieldDataType.Text, true),
            Field("status", "状态", ExportFieldDataType.Text, true), Field("result_date", "成果日期", ExportFieldDataType.Date, true),
            Field("quality_result", "质量结果", ExportFieldDataType.Text), Field("description", "说明", ExportFieldDataType.Text),
            Technical("_system_id", "系统ID"), Technical("_project_system_id", "项目系统ID"), Technical("_contract_system_id", "合同系统ID"), Technical("_concurrency_stamp", "并发版本"), Technical("_dataset_version", "数据集版本")
        ]),
        Define(ProjectWorkbookSheet.Receivables, "应收", true, false, [ProjectWorkbookSheet.ProjectMaster], FinanceFields(
            [Field("source_type", "来源", ExportFieldDataType.Text, true), Field("settlement_state", "结算状态", ExportFieldDataType.Text), Field("entry_date", "应收日期", ExportFieldDataType.Date, true), Field("original_amount", "原始金额", ExportFieldDataType.Number), Field("actual_amount", "实际金额", ExportFieldDataType.Number, CanImport: false, IsCalculated: true), Field("original_invoice_amount", "原始应开票金额", ExportFieldDataType.Number), Field("current_invoice_amount", "当前应开票金额", ExportFieldDataType.Number, CanImport: false, IsCalculated: true), Field("invoice_allocation_status", "发票分摊状态", ExportFieldDataType.Text, CanImport: false, IsCalculated: true), Field("cash_allocation_status", "资金分摊状态", ExportFieldDataType.Text, CanImport: false, IsCalculated: true), Field("amount", "金额", ExportFieldDataType.Number, true), Field("description", "说明", ExportFieldDataType.Text), Field("is_voided", "已作废", ExportFieldDataType.Boolean)])),
        Define(ProjectWorkbookSheet.Collections, "收款", true, false, [ProjectWorkbookSheet.Receivables], FinanceFields(
            [Field("receivable_id", "应收系统ID", ExportFieldDataType.Text), Field("collection_date", "收款日期", ExportFieldDataType.Date, true), Field("account_name", "收款账户", ExportFieldDataType.Text, CanImport: false), Field("account_id", "账户ID", ExportFieldDataType.Text, true), Field("amount", "金额", ExportFieldDataType.Number, true), Field("payment_method", "收款方式", ExportFieldDataType.Text, true), Field("notes", "备注", ExportFieldDataType.Text)])),
        Define(ProjectWorkbookSheet.Payables, "应付", true, false, [ProjectWorkbookSheet.ProjectMaster], FinanceFields(
            [Field("source_type", "来源", ExportFieldDataType.Text, true), Field("settlement_state", "结算状态", ExportFieldDataType.Text), Field("entry_date", "应付日期", ExportFieldDataType.Date, true), Field("original_amount", "原始金额", ExportFieldDataType.Number), Field("actual_amount", "实际金额", ExportFieldDataType.Number, CanImport: false, IsCalculated: true), Field("original_invoice_amount", "原始应开票金额", ExportFieldDataType.Number), Field("current_invoice_amount", "当前应开票金额", ExportFieldDataType.Number, CanImport: false, IsCalculated: true), Field("invoice_allocation_status", "发票分摊状态", ExportFieldDataType.Text, CanImport: false, IsCalculated: true), Field("cash_allocation_status", "资金分摊状态", ExportFieldDataType.Text, CanImport: false, IsCalculated: true), Field("amount", "金额", ExportFieldDataType.Number, true), Field("description", "说明", ExportFieldDataType.Text), Field("is_voided", "已作废", ExportFieldDataType.Boolean)])),
        Define(ProjectWorkbookSheet.Payments, "付款", true, false, [ProjectWorkbookSheet.Payables], FinanceFields(
            [Field("payable_id", "应付系统ID", ExportFieldDataType.Text), Field("payment_date", "付款日期", ExportFieldDataType.Date, true), Field("account_name", "付款账户", ExportFieldDataType.Text, CanImport: false), Field("account_id", "账户ID", ExportFieldDataType.Text, true), Field("amount", "金额", ExportFieldDataType.Number, true), Field("payment_method", "付款方式", ExportFieldDataType.Text, true), Field("notes", "备注", ExportFieldDataType.Text)])),
        Define(ProjectWorkbookSheet.Invoices, "发票", true, false, [ProjectWorkbookSheet.ProjectMaster], FinanceFields(
            [Field("direction", "发票方向", ExportFieldDataType.Text, true), Field("invoice_number", "发票号码", ExportFieldDataType.Text, true), Field("invoice_date", "发票日期", ExportFieldDataType.Date, true), Field("invoice_type", "发票类型", ExportFieldDataType.Text), Field("tax_rate", "税率", ExportFieldDataType.Number, true), Field("net_amount", "未税金额", ExportFieldDataType.Number, true), Field("tax_amount", "税额", ExportFieldDataType.Number, true), Field("gross_amount", "含税金额", ExportFieldDataType.Number, true), Field("status", "状态", ExportFieldDataType.Text, true)])),
        Define(ProjectWorkbookSheet.Deductions, "扣款", true, false, [ProjectWorkbookSheet.Receivables, ProjectWorkbookSheet.Payables], FinanceFields(
            [Field("settlement_id", "结算系统ID", ExportFieldDataType.Text, true), Field("deduction_date", "扣款日期", ExportFieldDataType.Date, true), Field("amount", "扣款金额", ExportFieldDataType.Number, true), Field("reduce_invoice_amount", "同时扣减应开票金额", ExportFieldDataType.Boolean, true), Field("reason", "扣款原因", ExportFieldDataType.Text, true), Field("status", "状态", ExportFieldDataType.Text, true)])),
        Define(ProjectWorkbookSheet.Attachments, "附件清单", true, true, [ProjectWorkbookSheet.ProjectMaster],
        [
            Field("project_number", "项目编号", ExportFieldDataType.Text, true), Field("contract_number", "合同编号", ExportFieldDataType.Text),
            Field("stage_result_id", "阶段成果系统ID", ExportFieldDataType.Text), Field("relation_type", "关联类型", ExportFieldDataType.Text, true),
            Field("original_file_name", "原文件名", ExportFieldDataType.Text, true), Field("content_type", "文件类型", ExportFieldDataType.Text),
            Field("category", "分类", ExportFieldDataType.Text, true), Field("description", "说明", ExportFieldDataType.Text),
            Field("size_bytes", "文件大小", ExportFieldDataType.Number, true, CanImport: false), Field("uploaded_at", "上传时间", ExportFieldDataType.Date, CanImport: false),
            Field("relative_path", "相对路径", ExportFieldDataType.Text, true), Field("sha256", "SHA-256", ExportFieldDataType.Text, true),
            Technical("_system_id", "系统ID"), Technical("_project_system_id", "项目系统ID"), Technical("_dataset_version", "数据集版本")
        ])
    ];

    public static ProjectWorkbookSheetDefinition Get(ProjectWorkbookSheet sheet) => Sheets.Single(item => item.Sheet == sheet);

    private static ProjectWorkbookSheetDefinition Define(
        ProjectWorkbookSheet sheet,
        string worksheetName,
        bool canImport,
        bool requiresArchive,
        IReadOnlyList<ProjectWorkbookSheet> dependsOn,
        IReadOnlyList<ProjectWorkbookFieldDefinition> fields) =>
        new(sheet, worksheetName, canImport, requiresArchive, dependsOn, fields);

    private static ProjectWorkbookFieldDefinition Field(
        string key,
        string header,
        ExportFieldDataType type,
        bool required = false,
        bool CanImport = true,
        bool CanExport = true,
        bool IsCalculated = false,
        bool IsSensitive = false,
        IReadOnlyList<string>? Aliases = null) =>
        new(key, header, type, required, CanImport, CanExport, false, IsCalculated, IsSensitive, Aliases);

    private static ProjectWorkbookFieldDefinition Technical(string key, string header) =>
        new(key, header, ExportFieldDataType.Text, false, true, true, true, false, false);

    private static IReadOnlyList<ProjectWorkbookFieldDefinition> FinanceFields(IReadOnlyList<ProjectWorkbookFieldDefinition> fields) =>
    [
        Field("project_number", "项目编号", ExportFieldDataType.Text, true),
        Field("contract_number", "合同编号", ExportFieldDataType.Text),
        Field("legal_entity_code", "签约公司编码", ExportFieldDataType.Text, true),
        Field("partner_number", "合作单位编号", ExportFieldDataType.Text),
        .. fields,
        Technical("_system_id", "系统ID"), Technical("_project_system_id", "项目系统ID"),
        Technical("_contract_system_id", "合同系统ID"), Technical("_concurrency_stamp", "并发版本"), Technical("_dataset_version", "数据集版本")
    ];
}
