using EngineeringManager.Application.Finance;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Finance;

namespace EngineeringManager.Web.Pages.Projects.Records;

public sealed record FinanceRecordEditorViewModel(
    Guid ProjectId, string Section, FinanceEntryKind Kind, Guid Id, DateOnly EntryDate, DateOnly? DueDate,
    decimal Amount, string? Description, PaymentMethod PaymentMethod, string? InvoiceNumber, decimal NetAmount,
    decimal TaxAmount, InvoiceStatus InvoiceStatus, Guid? ContractId, Guid? LegalEntityId, Guid? BusinessPartnerId,
    Guid? AccountId, Guid? RelatedEntryId, Guid? ProjectTaxConfigurationId, Guid ConcurrencyStamp)
{
    public static FinanceRecordEditorViewModel From(Guid projectId, ProjectReceivableItemDto row) =>
        new(projectId, "collection", FinanceEntryKind.Receivable, row.Id, row.EntryDate, row.DueDate, row.Amount, row.Description, default, null, 0m, 0m, default, row.ContractId, row.LegalEntityId, row.BusinessPartnerId, null, null, null, row.ConcurrencyStamp);
    public static FinanceRecordEditorViewModel From(Guid projectId, ProjectCollectionItemDto row) =>
        new(projectId, "collection", FinanceEntryKind.Collection, row.Id, row.CollectionDate, null, row.Amount, row.Notes, row.PaymentMethod, null, 0m, 0m, default, row.ContractId, row.LegalEntityId, row.BusinessPartnerId, row.AccountId, row.ReceivableEntryId, null, row.ConcurrencyStamp);
    public static FinanceRecordEditorViewModel From(Guid projectId, ProjectInvoiceItemDto row) =>
        new(projectId, "invoice", FinanceEntryKind.Invoice, row.Id, row.InvoiceDate, null, row.GrossAmount, null, default, row.InvoiceNumber, row.NetAmount, row.TaxAmount, row.Status, row.ContractId, row.LegalEntityId, row.BusinessPartnerId, null, null, row.ProjectTaxConfigurationId, row.ConcurrencyStamp);
    public static FinanceRecordEditorViewModel From(Guid projectId, ProjectPayableItemDto row) =>
        new(projectId, "payment", FinanceEntryKind.Payable, row.Id, row.EntryDate, row.DueDate, row.Amount, row.Description, default, null, 0m, 0m, default, row.ContractId, row.LegalEntityId, row.BusinessPartnerId, null, null, null, row.ConcurrencyStamp);
    public static FinanceRecordEditorViewModel From(Guid projectId, ProjectPaymentItemDto row) =>
        new(projectId, "payment", FinanceEntryKind.Payment, row.Id, row.PaymentDate, null, row.Amount, row.Notes, row.PaymentMethod, null, 0m, 0m, default, row.ContractId, row.LegalEntityId, row.BusinessPartnerId, row.AccountId, row.PayableEntryId, null, row.ConcurrencyStamp);
}
