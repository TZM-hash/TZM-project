using EngineeringManager.Domain.Employees;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.StageResults;

namespace EngineeringManager.Infrastructure.DataExchange;

internal static class DataExchangeValueLabels
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
        ExportDataset.CompanyCertificates => "公司证照",
        ExportDataset.Equipment => "设备档案",
        ExportDataset.EquipmentLeases => "设备租赁",
        ExportDataset.EquipmentUsages => "设备使用",
        ExportDataset.EquipmentPeriods => "设备使用期间",
        ExportDataset.EquipmentSettlements => "设备结算",
        ExportDataset.EmployeeCertificates => "员工证书",
        ExportDataset.EmployeeWages => "员工工资明细",
        ExportDataset.EmployeeOtherPayments => "员工其他往来",
        ExportDataset.EmployeeReceipts => "员工收款",
        ExportDataset.EmployeeFinancialAdjustments => "员工财务调整",
        _ => "数据集"
    };

    public static string ProjectAffiliation(ProjectAffiliationType value) => value switch
    {
        ProjectAffiliationType.SelfOperated => "自营项目",
        ProjectAffiliationType.ExternalPartyAttachedToUs => "他方挂靠我方",
        ProjectAffiliationType.WeAttachedToExternalParty => "我方挂靠他方",
        _ => "未知合作方式"
    };

    public static string LabelContractAllocationMode(ContractAllocationMode value) => value switch
    {
        ContractAllocationMode.SingleCompany => "单一公司",
        ContractAllocationMode.FixedAmount => "固定金额",
        ContractAllocationMode.Percentage => "按比例",
        ContractAllocationMode.LineItem => "按清单",
        _ => "未知分摊方式"
    };

    public static string LabelProjectAssignmentType(ProjectAssignmentType value) => value switch
    {
        ProjectAssignmentType.Responsible => "负责人",
        ProjectAssignmentType.Participant => "参与人员",
        ProjectAssignmentType.SiteStaff => "现场人员",
        _ => "未知人员类型"
    };

    public static string LabelProjectConstructionRecordType(ProjectConstructionRecordType value) => value switch
    {
        ProjectConstructionRecordType.Equipment => "设备",
        ProjectConstructionRecordType.ConstructionCrew => "施工班组",
        _ => "未知施工记录类型"
    };

    public static string LabelInvoiceDirection(InvoiceDirection value) => value switch
    {
        InvoiceDirection.Output => "销项",
        InvoiceDirection.Input => "进项",
        _ => "未知方向"
    };

    public static string LabelInvoiceStatus(InvoiceStatus value) => value switch
    {
        InvoiceStatus.Draft => "草稿",
        InvoiceStatus.IssuedOrReceived => "已开具/已收到",
        InvoiceStatus.Voided => "已作废",
        _ => "未知状态"
    };

    public static string LabelProjectStage(ProjectStage value) => value switch
    {
        ProjectStage.AwaitingMobilization => "待进场",
        ProjectStage.UnderConstruction => "施工中",
        ProjectStage.Suspended => "暂停施工",
        ProjectStage.CompletedUnsettled => "已完工未结算",
        ProjectStage.SettledArchived => "已结算归档",
        ProjectStage.PartiallySettled => "部分结算",
        _ => "未知阶段"
    };

    public static string LabelContractSigningStatus(ContractSigningStatus value) => value switch
    {
        ContractSigningStatus.NotSigned => "未签订",
        ContractSigningStatus.SentForSignature => "待签署",
        ContractSigningStatus.FullySigned => "已签订",
        ContractSigningStatus.NoContract => "不签合同",
        _ => "未知状态"
    };

    public static string LabelReceivableSourceType(ReceivableSourceType value) => value switch
    {
        ReceivableSourceType.ContractMilestone => "合同节点",
        ReceivableSourceType.StageSettlement => "阶段结算",
        ReceivableSourceType.Manual => "手工录入",
        _ => "未知来源"
    };

    public static string LabelPayableSourceType(PayableSourceType value) => value switch
    {
        PayableSourceType.Settlement => "结算",
        PayableSourceType.Contract => "合同",
        PayableSourceType.Manual => "手工录入",
        _ => "未知来源"
    };

    public static string LabelLedgerSettlementState(LedgerSettlementState value) => value switch
    {
        LedgerSettlementState.Provisional => "暂估",
        LedgerSettlementState.Final => "已定稿",
        _ => "未知结算状态"
    };

    public static string LabelLedgerSourceType(LedgerSourceType value) => value switch
    {
        LedgerSourceType.ProjectQuantity => "工程量",
        LedgerSourceType.Crew => "施工班组",
        LedgerSourceType.Partner => "合作单位",
        LedgerSourceType.CentralLedger => "中央账本",
        LedgerSourceType.LegacyMigration => "历史迁移",
        LedgerSourceType.ProjectCollection => "项目收款",
        _ => "未知来源"
    };

    public static string LabelLedgerAllocationStatus(LedgerAllocationStatus value) => value switch
    {
        LedgerAllocationStatus.Unallocated => "未分摊",
        LedgerAllocationStatus.PartiallyAllocated => "部分分摊",
        LedgerAllocationStatus.FullyAllocated => "已全部分摊",
        _ => "未知分摊状态"
    };

    public static string LabelProjectSettlementStatus(ProjectSettlementStatus value) => value switch
    {
        ProjectSettlementStatus.Estimated => "预计",
        ProjectSettlementStatus.PartiallySettled => "部分结算",
        ProjectSettlementStatus.Settled => "已结算",
        _ => "未知状态"
    };

    public static string LabelContractType(ContractType value) => value switch
    {
        ContractType.MainContract => "主合同",
        ContractType.Supplement => "补充协议",
        ContractType.ChangeOrder => "变更单",
        ContractType.Subcontract => "分包合同",
        ContractType.SupplierContract => "供应商合同",
        _ => "未知合同类型"
    };

    public static string LabelStageResultType(StageResultType value) => value switch
    {
        StageResultType.Progress => "进度",
        StageResultType.Acceptance => "验收",
        StageResultType.Completion => "完工",
        StageResultType.SettlementSupport => "结算支撑",
        _ => "未知成果类型"
    };

    public static string LabelStageResultStatus(StageResultStatus value) => value switch
    {
        StageResultStatus.Draft => "草稿",
        StageResultStatus.Recorded => "已记录",
        StageResultStatus.Voided => "已作废",
        _ => "未知状态"
    };

    public static string LabelQualityResult(QualityResult value) => value switch
    {
        QualityResult.NotChecked => "未检查",
        QualityResult.Qualified => "合格",
        QualityResult.ConditionallyQualified => "有条件合格",
        QualityResult.Unqualified => "不合格",
        _ => "未知结果"
    };

    public static string BusinessPartnerRole(BusinessPartnerRoleType value) => value switch
    {
        BusinessPartnerRoleType.CustomerOrGeneralContractor => "客户/总包",
        BusinessPartnerRoleType.ConstructionCrew => "施工班组",
        BusinessPartnerRoleType.MaterialSupplier => "材料供应商",
        BusinessPartnerRoleType.MiscellaneousSupplier => "其他供应商",
        _ => "未知角色"
    };

    public static string LabelEmployeeType(EmployeeType value) => value switch
    {
        EmployeeType.Formal => "正式员工",
        EmployeeType.Labor => "劳务员工",
        EmployeeType.Temporary => "特殊临时人员",
        _ => "未知人员类型"
    };

    public static string LabelPayrollBatchType(PayrollBatchType value) => value switch
    {
        PayrollBatchType.Monthly => "按月",
        PayrollBatchType.DateRange => "按日期范围",
        PayrollBatchType.ProjectStage => "项目阶段",
        PayrollBatchType.Milestone => "里程碑",
        PayrollBatchType.Temporary => "临时",
        _ => "未知批次类型"
    };

    public static string LabelPayrollRecipientType(PayrollRecipientType value) => value switch
    {
        PayrollRecipientType.Employee => "员工",
        PayrollRecipientType.CrewWorker => "班组工人",
        _ => "未知人员来源"
    };

    public static string LabelPaymentMethod(PaymentMethod value) => value switch
    {
        PaymentMethod.BankTransfer => "银行转账",
        PaymentMethod.Cash => "现金",
        PaymentMethod.WeChat => "微信",
        PaymentMethod.Alipay => "支付宝",
        PaymentMethod.Other => "其他",
        _ => "未知方式"
    };

    public static string LabelPaymentMethod(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.Trim() switch
        {
            "银行转账" or "BankTransfer" or "银行" => "银行转账",
            "现金" or "Cash" => "现金",
            "微信" or "WeChat" => "微信",
            "支付宝" or "Alipay" => "支付宝",
            "其他" or "Other" => "其他",
            _ => "其他"
        };
    }

    public static string LabelInvoiceType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.Trim() switch
        {
            "Ordinary" or "普通发票" or "普票" => "普票",
            "Special" or "专用发票" or "专票" => "专票",
            _ => value.Trim()
        };
    }

    public static string LabelFinancialAccountType(FinancialAccountType value) => value switch
    {
        FinancialAccountType.Bank => "银行",
        FinancialAccountType.Cash => "现金",
        FinancialAccountType.Other => "其他",
        FinancialAccountType.PersonalAdvance => "个人垫付",
        _ => "未知账户类型"
    };

    public static string LabelLedgerDirection(LedgerDirection value) => value switch
    {
        LedgerDirection.Receivable => "应收",
        LedgerDirection.Payable => "应付",
        _ => "未知方向"
    };

    public static string LabelLedgerRecordStatus(LedgerRecordStatus value) => value switch
    {
        LedgerRecordStatus.Active => "有效",
        LedgerRecordStatus.Voided => "已作废",
        _ => "未知状态"
    };

    public static string EquipmentOwnership(EquipmentOwnershipType value) => value switch
    {
        EquipmentOwnershipType.SelfOwned => "自有",
        EquipmentOwnershipType.Rented => "租赁",
        EquipmentOwnershipType.Other => "其他",
        _ => "未知权属"
    };

    public static string LabelEquipmentStatus(EquipmentStatus value) => value switch
    {
        EquipmentStatus.Idle => "闲置",
        EquipmentStatus.InUse => "使用中",
        EquipmentStatus.Maintenance => "维修中",
        EquipmentStatus.Disabled => "停用",
        EquipmentStatus.Scrapped => "已报废",
        EquipmentStatus.TransferredOut => "已调出",
        _ => "未知状态"
    };

    public static string LabelRentMode(RentMode value) => value switch
    {
        RentMode.Daily => "日租",
        RentMode.Monthly => "月租",
        RentMode.StagePackage => "阶段包干",
        _ => "未知计租方式"
    };

    public static string LabelEquipmentPeriodType(EquipmentPeriodType value) => value switch
    {
        EquipmentPeriodType.Work => "施工",
        EquipmentPeriodType.Stop => "停工",
        _ => "未知日期段类型"
    };

    public static string LabelAttachmentCategory(AttachmentCategory value) => value switch
    {
        AttachmentCategory.General => "通用",
        AttachmentCategory.Photo => "照片",
        AttachmentCategory.Acceptance => "验收",
        AttachmentCategory.Completion => "完工",
        AttachmentCategory.Contract => "合同",
        AttachmentCategory.Quantity => "工程量",
        _ => "未知附件分类"
    };

    public static string LabelEmployeeWageCategory(EmployeeWageCategory value) => value switch
    {
        EmployeeWageCategory.SocialSecurityWage => "社保工资",
        EmployeeWageCategory.MigrantWorkerWage => "农民工工资",
        EmployeeWageCategory.OtherWage => "其他工资",
        _ => "未知工资类别"
    };

    public static string LabelEmployeeWageCalculationMethod(EmployeeWageCalculationMethod value) => value switch
    {
        EmployeeWageCalculationMethod.Monthly => "按月",
        EmployeeWageCalculationMethod.Daily => "按日",
        EmployeeWageCalculationMethod.Hourly => "按小时",
        EmployeeWageCalculationMethod.Piecework => "按计件",
        EmployeeWageCalculationMethod.FixedAmount => "固定金额",
        EmployeeWageCalculationMethod.CustomUnit => "自定义单位",
        _ => "未知计薪方式"
    };

    public static string LabelEmployeeWageEntryType(EmployeeWageEntryType value) => value switch
    {
        EmployeeWageEntryType.Attendance => "出勤",
        EmployeeWageEntryType.Overtime => "加班",
        EmployeeWageEntryType.Bonus => "奖金",
        EmployeeWageEntryType.Penalty => "罚款",
        EmployeeWageEntryType.Other => "其他",
        _ => "未知工资明细类型"
    };

    public static string LabelPayrollItemType(PayrollItemType value) => value switch
    {
        PayrollItemType.FixedSalary => "固定工资",
        PayrollItemType.DailyWage => "日工资",
        PayrollItemType.HourlyWage => "时工资",
        PayrollItemType.Piecework => "计件工资",
        PayrollItemType.LumpSum => "包干工资",
        PayrollItemType.Overtime => "加班",
        PayrollItemType.Bonus => "奖金",
        PayrollItemType.Allowance => "津贴",
        PayrollItemType.AdvanceDeduction => "借支抵扣",
        PayrollItemType.LeaveDeduction => "请假扣款",
        PayrollItemType.Penalty => "罚款",
        PayrollItemType.OtherDeduction => "其他扣款",
        PayrollItemType.BackPay => "补发",
        PayrollItemType.Reversal => "冲销",
        _ => "未知工资项目类型"
    };

    public static string LabelPayrollItemNature(PayrollItemNature value) => value switch
    {
        PayrollItemNature.Earning => "收入",
        PayrollItemNature.Deduction => "扣款",
        _ => "未知收支性质"
    };

    public static string LabelEmployeeLedgerEntryType(EmployeeLedgerEntryType value) => value switch
    {
        EmployeeLedgerEntryType.Expense => "费用",
        EmployeeLedgerEntryType.AdvanceDisbursement => "借支发放",
        EmployeeLedgerEntryType.AdvanceRepayment => "借支归还",
        EmployeeLedgerEntryType.Dividend => "分红",
        EmployeeLedgerEntryType.Interest => "利息",
        EmployeeLedgerEntryType.Other => "其他",
        _ => "未知往来类型"
    };

    public static string LabelEmployeeLedgerRecordKind(EmployeeLedgerRecordKind value) => value switch
    {
        EmployeeLedgerRecordKind.Payable => "应付",
        EmployeeLedgerRecordKind.Payment => "已付款",
        EmployeeLedgerRecordKind.RefundOrReversal => "退款/冲销",
        _ => "未知记录性质"
    };

    public static string LabelEmployeeReceiptType(EmployeeReceiptType value) => value switch
    {
        EmployeeReceiptType.Wage => "工资",
        EmployeeReceiptType.Expense => "报销",
        EmployeeReceiptType.DividendOrOther => "分红/其他",
        EmployeeReceiptType.Advance => "借支",
        EmployeeReceiptType.General => "通用",
        _ => "未知收款类型"
    };

    public static string LabelEmployeeFinancialAdjustmentType(EmployeeFinancialAdjustmentType value) => value switch
    {
        EmployeeFinancialAdjustmentType.AdministratorAdjustment => "管理员调整",
        EmployeeFinancialAdjustmentType.HistoricalOpeningBalance => "历史期初余额",
        EmployeeFinancialAdjustmentType.Reversal => "冲销",
        _ => "未知调整类型"
    };

    public static string? NormalizeEnumValue(ProjectWorkbookSheet sheet, string key, string? value) => (sheet, key) switch
    {
        (ProjectWorkbookSheet.ProjectMaster, "stage") => Normalize<ProjectStage>(value, LabelProjectStage),
        (ProjectWorkbookSheet.ProjectMaster, "contract_signing_status") => Normalize<ContractSigningStatus>(value, LabelContractSigningStatus),
        (ProjectWorkbookSheet.ProjectMaster, "affiliation_type") => Normalize<ProjectAffiliationType>(value, ProjectAffiliation),
        (ProjectWorkbookSheet.Contracts, "contract_type") => Normalize<ContractType>(value, LabelContractType),
        (ProjectWorkbookSheet.Contracts, "allocation_mode") => Normalize<ContractAllocationMode>(value, LabelContractAllocationMode),
        (ProjectWorkbookSheet.Assignments, "assignment_type") => Normalize<ProjectAssignmentType>(value, LabelProjectAssignmentType),
        (ProjectWorkbookSheet.Partners, "role_type") => Normalize<BusinessPartnerRoleType>(value, BusinessPartnerRole),
        (ProjectWorkbookSheet.Construction, "record_type") => Normalize<ProjectConstructionRecordType>(value, LabelProjectConstructionRecordType),
        (ProjectWorkbookSheet.StageResults, "result_type") => Normalize<StageResultType>(value, LabelStageResultType),
        (ProjectWorkbookSheet.StageResults, "status") => Normalize<StageResultStatus>(value, LabelStageResultStatus),
        (ProjectWorkbookSheet.StageResults, "quality_result") => Normalize<QualityResult>(value, LabelQualityResult),
        (ProjectWorkbookSheet.Receivables or ProjectWorkbookSheet.Payables, "source_type") => NormalizeSourceType(value),
        (ProjectWorkbookSheet.Receivables or ProjectWorkbookSheet.Payables, "settlement_state") => Normalize<LedgerSettlementState>(value, LabelLedgerSettlementState),
        (ProjectWorkbookSheet.Collections or ProjectWorkbookSheet.Payments, "payment_method") => Normalize<PaymentMethod>(value, LabelPaymentMethod),
        (ProjectWorkbookSheet.Invoices, "status") => NormalizeInvoiceStatus(value),
        (ProjectWorkbookSheet.Deductions, "status") => Normalize<LedgerRecordStatus>(value, LabelLedgerRecordStatus),
        (ProjectWorkbookSheet.Attachments, "category") => Normalize<AttachmentCategory>(value, LabelAttachmentCategory),
        _ => value?.Trim()
    };

    private static string? NormalizeSourceType(string? value)
    {
        if (TryNormalize<LedgerSourceType>(value, LabelLedgerSourceType, out var normalized)) return normalized;
        if (TryNormalize<ReceivableSourceType>(value, LabelReceivableSourceType, out normalized)) return normalized;
        if (TryNormalize<PayableSourceType>(value, LabelPayableSourceType, out normalized)) return normalized;
        return value?.Trim();
    }

    private static string? NormalizeInvoiceStatus(string? value)
    {
        if (TryNormalize<InvoiceStatus>(value, LabelInvoiceStatus, out var normalized)) return normalized;
        if (TryNormalize<LedgerRecordStatus>(value, LabelLedgerRecordStatus, out normalized)) return normalized;
        return value?.Trim();
    }

    private static string? Normalize<TEnum>(string? value, Func<TEnum, string> label) where TEnum : struct, Enum =>
        TryNormalize(value, label, out var normalized) ? normalized : value?.Trim();

    private static bool TryNormalize<TEnum>(string? value, Func<TEnum, string> label, out string normalized) where TEnum : struct, Enum
    {
        normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (Enum.TryParse<TEnum>(value, true, out var parsed) && Enum.IsDefined(parsed))
        {
            normalized = parsed.ToString();
            return true;
        }
        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (string.Equals(label(candidate), value.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                normalized = candidate.ToString();
                return true;
            }
        }
        return false;
    }
}
