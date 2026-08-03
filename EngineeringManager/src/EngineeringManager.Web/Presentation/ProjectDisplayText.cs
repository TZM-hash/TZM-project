using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.StageResults;
using System.Globalization;

namespace EngineeringManager.Web.Presentation;

public static class ProjectDisplayText
{
    public static string ToChinese(this EmployeeType value) => value switch
    {
        EmployeeType.Formal => "正式员工",
        EmployeeType.Labor => "劳务员工",
        EmployeeType.Temporary => "特殊临时人员",
        _ => "未知人员类型"
    };

    public static string ToChinese(this BusinessPartnerRoleType value) => value switch
    {
        BusinessPartnerRoleType.CustomerOrGeneralContractor => "甲方/总包",
        BusinessPartnerRoleType.ConstructionCrew => "施工班组",
        BusinessPartnerRoleType.MaterialSupplier => "材料供应商",
        BusinessPartnerRoleType.MiscellaneousSupplier => "零星供应商",
        _ => "未知角色"
    };

    public static string ToChinese(this EquipmentOwnershipType value) => value switch
    {
        EquipmentOwnershipType.SelfOwned => "自有设备",
        EquipmentOwnershipType.Rented => "租赁设备",
        EquipmentOwnershipType.Other => "其他",
        _ => "未知权属"
    };

    public static string ToChinese(this EquipmentStatus value) => value switch
    {
        EquipmentStatus.Idle => "闲置",
        EquipmentStatus.InUse => "使用中",
        EquipmentStatus.Maintenance => "维修中",
        EquipmentStatus.Disabled => "停用",
        EquipmentStatus.Scrapped => "已报废",
        EquipmentStatus.TransferredOut => "已调出",
        _ => "未知状态"
    };

    public static string ToChinese(this InvoiceDirection value) => value switch
    {
        InvoiceDirection.Output => "销项发票",
        InvoiceDirection.Input => "进项发票",
        _ => "未知发票方向"
    };

    public static string ToChinese(this InvoiceStatus value) => value switch
    {
        InvoiceStatus.Draft => "草稿",
        InvoiceStatus.IssuedOrReceived => "已开具/已收到",
        InvoiceStatus.Voided => "已作废",
        _ => "未知发票状态"
    };

    public static string ToChinese(this PaymentMethod value) => value switch
    {
        PaymentMethod.BankTransfer => "银行转账",
        PaymentMethod.Cash => "现金",
        PaymentMethod.WeChat => "微信",
        PaymentMethod.Alipay => "支付宝",
        PaymentMethod.Other => "其他",
        _ => "未知方式"
    };

    public static string PaymentMethodLabel(string? value) => value switch
    {
        nameof(PaymentMethod.BankTransfer) => "银行转账",
        nameof(PaymentMethod.Cash) => "现金",
        nameof(PaymentMethod.WeChat) => "微信",
        nameof(PaymentMethod.Alipay) => "支付宝",
        nameof(PaymentMethod.Other) => "其他",
        null or "" => "-",
        _ => value
    };

    public static string DateLabel(DateOnly value) => value == DateOnly.MinValue
        ? "日期待确认"
        : value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string ActivityDateLabel(DateTimeOffset value) => value.Year == 1 && value.Month == 1 && value.Day == 1
        ? "日期待确认"
        : value.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);

    public static string InvoiceTypeLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "-";
        if (Enum.TryParse<ProjectInvoiceType>(value, true, out var invoiceType)) return invoiceType.ToChinese();
        return Enum.TryParse<InvoiceDirection>(value, true, out var direction)
            ? direction.ToChinese()
            : value.Trim();
    }

    public static string ToChinese(this ProjectStage value) => value switch
    {
        ProjectStage.AwaitingMobilization => "待进场",
        ProjectStage.UnderConstruction => "施工中",
        ProjectStage.Suspended => "停工中",
        ProjectStage.CompletedUnsettled => "已完工未结算",
        ProjectStage.PartiallySettled => "部分结算",
        ProjectStage.SettledArchived => "已结算归档",
        _ => "未知阶段"
    };

    public static string ToChinese(this ContractSigningStatus value) => value switch
    {
        ContractSigningStatus.NotSigned => "未签合同",
        ContractSigningStatus.SentForSignature => "合同已寄出",
        ContractSigningStatus.FullySigned => "合同已签完",
        ContractSigningStatus.NoContract => "不签合同",
        _ => "未知合同状态"
    };

    public static string ToChinese(this ProjectInvoiceType value) => value switch
    {
        ProjectInvoiceType.Ordinary => "普票",
        ProjectInvoiceType.Special => "专票",
        _ => "未知发票类型"
    };

    public static string ToChinese(this ProjectSettlementStatus value) => value switch
    {
        ProjectSettlementStatus.Estimated => "暂估",
        ProjectSettlementStatus.PartiallySettled => "部分结算",
        ProjectSettlementStatus.Settled => "已结算",
        _ => "未知结算状态"
    };

    public static string ToChinese(this ProjectAffiliationType value) => value switch
    {
        ProjectAffiliationType.SelfOperated => "自营项目",
        ProjectAffiliationType.ExternalPartyAttachedToUs => "他方挂靠我方",
        ProjectAffiliationType.WeAttachedToExternalParty => "我方挂靠他方",
        _ => "未知合作方式"
    };

    public static string ToChinese(this FinancialAccountType value) => value switch
    {
        FinancialAccountType.Bank => "银行",
        FinancialAccountType.Cash => "现金",
        FinancialAccountType.Other => "其他",
        FinancialAccountType.PersonalAdvance => "个人垫付账户",
        _ => "未知账户类型"
    };

    public static string ToChinese(this StageResultType value) => value switch
    {
        StageResultType.Progress => "进度",
        StageResultType.Acceptance => "验收",
        StageResultType.Completion => "完工",
        StageResultType.SettlementSupport => "结算支撑",
        _ => "未知成果类型"
    };

    public static string ToChinese(this StageResultStatus value) => value switch
    {
        StageResultStatus.Draft => "草稿",
        StageResultStatus.Recorded => "已记录",
        StageResultStatus.Voided => "已作废",
        _ => "未知状态"
    };

    public static string ToChinese(this QualityResult value) => value switch
    {
        QualityResult.NotChecked => "未检查",
        QualityResult.Qualified => "合格",
        QualityResult.ConditionallyQualified => "有条件合格",
        QualityResult.Unqualified => "不合格",
        _ => "未知结果"
    };
}
