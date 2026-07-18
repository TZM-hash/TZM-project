using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Finance.Entries;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance)]
public sealed class CreateModel(IFinanceLedgerService financeService) : PageModel
{
    public FinanceEntryOptionsDto Options { get; private set; } = new([], [], [], [], [], [], [], [], []);

    [BindProperty(SupportsGet = true)] public FinanceEntryKind EntryKind { get; set; } = FinanceEntryKind.Receivable;
    [BindProperty(SupportsGet = true)] public Guid ProjectId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? ContractId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid LegalEntityId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? BusinessPartnerId { get; set; }
    [BindProperty] public Guid? AccountId { get; set; }
    [BindProperty] public Guid? SecondaryAccountId { get; set; }
    [BindProperty] public Guid? RelatedEntryId { get; set; }
    [BindProperty] public DateOnly EntryDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [BindProperty] public DateOnly? DueDate { get; set; }
    [BindProperty] public decimal Amount { get; set; }
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;
    [BindProperty] public InvoiceDirection InvoiceDirection { get; set; } = InvoiceDirection.Output;
    [BindProperty] public string? InvoiceNumber { get; set; }
    [BindProperty] public Guid? ProjectTaxConfigurationId { get; set; }
    [BindProperty] public decimal NetAmount { get; set; }
    [BindProperty] public decimal TaxAmount { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Options = await financeService.GetEntryOptionsAsync(cancellationToken);
        if (ProjectId != Guid.Empty)
        {
            if (LegalEntityId == Guid.Empty) LegalEntityId = Options.ProjectLegalEntities?.FirstOrDefault(item => item.ParentId == ProjectId)?.Id ?? Guid.Empty;
            ProjectTaxConfigurationId ??= Options.ProjectTaxConfigurations?.FirstOrDefault(item => item.ParentId == ProjectId)?.Id;
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveAsync(cancellationToken);
            return !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
                ? LocalRedirect(ReturnUrl)
                : RedirectToPage("/Finance/Index");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            Options = await financeService.GetEntryOptionsAsync(cancellationToken);
            return Page();
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        switch (EntryKind)
        {
            case FinanceEntryKind.Receivable:
                await financeService.AddReceivableAsync(new CreateReceivableRequest(ProjectId, ContractId, LegalEntityId, BusinessPartnerId, ReceivableSourceType.Manual, EntryDate, DueDate, Amount, Description), cancellationToken);
                break;
            case FinanceEntryKind.Collection:
                await financeService.RecordCollectionAsync(new RecordCollectionRequest(RelatedEntryId, ProjectId, ContractId, LegalEntityId, BusinessPartnerId, Required(AccountId, "请选择收款账户。"), EntryDate, Amount, PaymentMethod, Description), cancellationToken);
                break;
            case FinanceEntryKind.RefundOrCollectionReversal:
                await financeService.RecordRefundAsync(new RecordRefundRequest(RelatedEntryId, null, Required(AccountId, "请选择原收款账户。"), EntryDate, Amount, FinancialAdjustmentType.Refund, RequiredText(Description, "请填写退款或冲销原因。")), cancellationToken);
                break;
            case FinanceEntryKind.Payable:
                await financeService.AddPayableAsync(new CreatePayableRequest(ProjectId, ContractId, LegalEntityId, Required(BusinessPartnerId, "请选择合作单位。"), PayableSourceType.Manual, EntryDate, DueDate, Amount, Description), cancellationToken);
                break;
            case FinanceEntryKind.Payment:
                await financeService.RecordPaymentAsync(new RecordPaymentRequest(RelatedEntryId, ProjectId, ContractId, LegalEntityId, Required(BusinessPartnerId, "请选择合作单位。"), Required(AccountId, "请选择付款账户。"), EntryDate, Amount, PaymentMethod, Description), cancellationToken);
                break;
            case FinanceEntryKind.Deduction:
                await financeService.AddDeductionAsync(new CreateDeductionRequest(Required(RelatedEntryId, "请选择应付记录。"), ProjectId, LegalEntityId, Required(BusinessPartnerId, "请选择合作单位。"), EntryDate, Amount, RequiredText(Description, "请填写扣款原因。")), cancellationToken);
                break;
            case FinanceEntryKind.PaymentReversal:
                await financeService.RecordPaymentReversalAsync(new RecordPaymentReversalRequest(Required(RelatedEntryId, "请选择原付款记录。"), Required(AccountId, "请选择原付款账户。"), EntryDate, Amount, FinancialAdjustmentType.Reversal, RequiredText(Description, "请填写付款冲销原因。")), cancellationToken);
                break;
            case FinanceEntryKind.Transfer:
                await financeService.TransferAsync(new CreateAccountTransferRequest(Required(AccountId, "请选择转出账户。"), Required(SecondaryAccountId, "请选择转入账户。"), EntryDate, Amount, Description), cancellationToken);
                break;
            case FinanceEntryKind.Invoice:
                await financeService.AddInvoiceAsync(new CreateInvoiceRequest(ProjectId, ContractId, LegalEntityId, BusinessPartnerId, InvoiceDirection, RequiredText(InvoiceNumber, "请填写发票号码。"), EntryDate, Required(ProjectTaxConfigurationId, "请选择税率和发票类型。"), NetAmount, TaxAmount, Amount, InvoiceStatus.IssuedOrReceived, [], []), cancellationToken);
                break;
            default:
                throw new InvalidOperationException("不支持的财务业务类型。");
        }
    }

    private static Guid Required(Guid? value, string message) => value is { } id && id != Guid.Empty ? id : throw new ArgumentException(message);
    private static string RequiredText(string? value, string message) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new ArgumentException(message);
}
