using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;

namespace EngineeringManager.Web.Presentation;

public static class ProjectDisplayText
{
    public static string ToChinese(this EmployeeType value) => value switch
    {
        EmployeeType.Formal => "正式员工",
        EmployeeType.Labor => "劳务员工",
        EmployeeType.Temporary => "特殊临时人员",
        _ => value.ToString()
    };

    public static string ToChinese(this BusinessPartnerRoleType value) => value switch
    {
        BusinessPartnerRoleType.CustomerOrGeneralContractor => "甲方/总包",
        BusinessPartnerRoleType.ConstructionCrew => "施工班组",
        BusinessPartnerRoleType.MaterialSupplier => "材料供应商",
        BusinessPartnerRoleType.MiscellaneousSupplier => "零星供应商",
        _ => value.ToString()
    };

    public static string ToChinese(this EquipmentOwnershipType value) => value switch
    {
        EquipmentOwnershipType.SelfOwned => "自有设备",
        EquipmentOwnershipType.Rented => "租赁设备",
        _ => value.ToString()
    };

    public static string ToChinese(this EquipmentStatus value) => value switch
    {
        EquipmentStatus.Idle => "闲置",
        EquipmentStatus.InUse => "使用中",
        EquipmentStatus.Maintenance => "维修中",
        EquipmentStatus.Disabled => "停用",
        EquipmentStatus.Scrapped => "已报废",
        EquipmentStatus.TransferredOut => "已调出",
        _ => value.ToString()
    };

    public static string ToChinese(this InvoiceDirection value) => value switch
    {
        InvoiceDirection.Output => "销项发票",
        InvoiceDirection.Input => "进项发票",
        _ => value.ToString()
    };

    public static string ToChinese(this InvoiceStatus value) => value switch
    {
        InvoiceStatus.Draft => "草稿",
        InvoiceStatus.IssuedOrReceived => "已开具/已收到",
        InvoiceStatus.Voided => "已作废",
        _ => value.ToString()
    };

    public static string ToChinese(this PaymentMethod value) => value switch
    {
        PaymentMethod.BankTransfer => "银行转账",
        PaymentMethod.Cash => "现金",
        PaymentMethod.WeChat => "微信",
        PaymentMethod.Alipay => "支付宝",
        PaymentMethod.Other => "其他",
        _ => value.ToString()
    };

    public static string ToChinese(this ProjectStage value) => value switch
    {
        ProjectStage.AwaitingMobilization => "待进场",
        ProjectStage.UnderConstruction => "施工中",
        ProjectStage.Suspended => "停工中",
        ProjectStage.CompletedUnsettled => "已完工未结算",
        ProjectStage.SettledArchived => "已结算归档",
        _ => value.ToString()
    };

    public static string ToChinese(this ContractSigningStatus value) => value switch
    {
        ContractSigningStatus.NotSigned => "未签合同",
        ContractSigningStatus.SentForSignature => "合同已寄出",
        ContractSigningStatus.FullySigned => "合同已签完",
        _ => value.ToString()
    };

    public static string ToChinese(this ProjectInvoiceType value) => value switch
    {
        ProjectInvoiceType.Ordinary => "普票",
        ProjectInvoiceType.Special => "专票",
        _ => value.ToString()
    };

    public static string ToChinese(this ProjectSettlementStatus value) => value switch
    {
        ProjectSettlementStatus.Estimated => "暂估",
        ProjectSettlementStatus.PartiallySettled => "部分结算",
        ProjectSettlementStatus.Settled => "已结算",
        _ => value.ToString()
    };

    public static string ToChinese(this ProjectAffiliationType value) => value switch
    {
        ProjectAffiliationType.SelfOperated => "自营项目",
        ProjectAffiliationType.ExternalPartyAttachedToUs => "他方挂靠我方",
        ProjectAffiliationType.WeAttachedToExternalParty => "我方挂靠他方",
        _ => value.ToString()
    };
}
