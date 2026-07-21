using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EngineeringManager.Web.Pages.Ledger.Entries;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance)]
public sealed class EditModel(ICentralLedgerCommandService commands, ICentralLedgerQueryService queries, ApplicationDbContext db) : PageModel
{
    public CentralLedgerOptionsDto Options { get; private set; } = new([], [], [], [], [], [], [], [], []);

    [BindProperty(SupportsGet = true)] public LedgerScope Scope { get; set; } = LedgerScope.External;
    [BindProperty(SupportsGet = true)] public FinanceRecordType RecordType { get; set; } = FinanceRecordType.Settlement;
    [BindProperty(SupportsGet = true)] public LedgerDirection Direction { get; set; } = LedgerDirection.Receivable;
    [BindProperty] public LedgerSettlementState SettlementState { get; set; } = LedgerSettlementState.Final;
    [BindProperty] public Guid LegalEntityId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? BusinessPartnerId { get; set; }
    [BindProperty] public Guid? CounterLegalEntityId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? ProjectId { get; set; }
    [BindProperty] public Guid? ContractId { get; set; }
    [BindProperty] public Guid? SettlementId { get; set; }
    [BindProperty] public Guid SettlementConcurrencyStamp { get; set; }
    [BindProperty] public Guid? AccountId { get; set; }
    [BindProperty] public Guid? CounterAccountId { get; set; }
    [BindProperty] public DateOnly BusinessDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [BindProperty] public decimal Amount { get; set; }
    [BindProperty] public decimal InvoiceAmount { get; set; }
    [BindProperty] public bool ReduceInvoiceAmount { get; set; }
    [BindProperty] public string? InvoiceNumber { get; set; }
    [BindProperty] public string? InvoiceType { get; set; }
    [BindProperty] public decimal? NetAmount { get; set; }
    [BindProperty] public decimal? TaxAmount { get; set; }
    [BindProperty] public decimal? TaxRate { get; set; }
    [BindProperty] public string? PaymentMethod { get; set; }
    [BindProperty] public string? Notes { get; set; }
    [BindProperty] public string Reason { get; set; } = string.Empty;
    [BindProperty] public bool AutoAllocate { get; set; }
    [BindProperty] public List<AllocationInput> Allocations { get; set; } = [new()];
    [BindProperty] public FinanceRecordType DeleteRecordType { get; set; }
    [BindProperty] public Guid DeleteRecordId { get; set; }
    [BindProperty] public Guid DeleteConcurrencyStamp { get; set; }
    [BindProperty] public string DeleteReason { get; set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken token) => await LoadOptionsAsync(token);

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken token)
    {
        try
        {
            if (ProjectId.HasValue && ProjectId.Value != Guid.Empty && RecordType == FinanceRecordType.Settlement && Direction == LedgerDirection.Receivable)
                throw new InvalidOperationException("项目应收由工程量明细自动生成，不能手工新增。");
            var actor = await LedgerPageSupport.CreateActorAsync(User, db, token);
            switch (RecordType)
            {
                case FinanceRecordType.Settlement:
                    await commands.CreateSettlementAsync(actor, new CreateSettlementRequest(
                        Scope, Direction, SettlementState, LedgerSourceType.CentralLedger, null, LegalEntityId, BusinessPartnerId,
                        CounterLegalEntityId, ProjectId, ContractId, null, BusinessDate, Amount,
                        InvoiceAmount == 0m ? Amount : InvoiceAmount, Notes), token);
                    break;
                case FinanceRecordType.Deduction:
                    await commands.AddDeductionAsync(actor, new AddFinanceDeductionRequest(
                        SettlementId ?? throw new ArgumentException("扣款必须填写结算 ID。"), BusinessDate, Amount,
                        ReduceInvoiceAmount, Reason, SettlementConcurrencyStamp), token);
                    break;
                case FinanceRecordType.Invoice:
                    await commands.CreateInvoiceAsync(actor, new CreateFinanceInvoiceRequest(
                        Scope, Direction, LedgerSourceType.CentralLedger, null, LegalEntityId, BusinessPartnerId,
                        CounterLegalEntityId, InvoiceNumber ?? string.Empty, BusinessDate, Amount, NetAmount, TaxAmount, TaxRate,
                        Notes, ToAllocations(), AutoAllocate, InvoiceType: InvoiceType), token);
                    break;
                case FinanceRecordType.Cash:
                    await commands.CreateCashAsync(actor, new CreateFinanceCashRequest(
                        Scope, Direction, Scope == LedgerScope.Internal ? LedgerCashType.InternalTransfer : Direction == LedgerDirection.Receivable ? LedgerCashType.Collection : LedgerCashType.Payment,
                        LedgerSourceType.CentralLedger, null, LegalEntityId, BusinessPartnerId, CounterLegalEntityId, AccountId,
                        CounterAccountId, BusinessDate, Amount, PaymentMethod, Notes, ToAllocations(), AutoAllocate), token);
                    break;
                default:
                    throw new ArgumentException("当前页面不支持该财务记录类型。");
            }
            TempData["Success"] = "财务记录已保存到中央账本。";
            return RedirectToLedger();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadOptionsAsync(token);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(CancellationToken token)
    {
        try
        {
            var actor = await LedgerPageSupport.CreateActorAsync(User, db, token);
            await commands.DeleteAsync(actor, new DeleteFinanceRecordRequest(
                DeleteRecordType, DeleteRecordId, DeleteConcurrencyStamp, DeleteReason, "CentralLedger"), token);
            TempData["Success"] = "财务记录已物理删除，删除日志已保留。";
            return RedirectToLedger();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadOptionsAsync(token);
            return Page();
        }
    }

    private FinanceAllocationRequest[] ToAllocations() => Allocations
        .Where(item => item.SettlementId != Guid.Empty && item.Amount > 0m)
        .Select((item, index) => new FinanceAllocationRequest(item.SettlementId, item.Amount, index + 1))
        .ToArray();

    private async Task LoadOptionsAsync(CancellationToken token)
    {
        var actor = await LedgerPageSupport.CreateActorAsync(User, db, token);
        Options = await queries.GetOptionsAsync(actor, Scope, token);
    }

    private RedirectToPageResult RedirectToLedger() => RedirectToPage(Scope == LedgerScope.Internal ? "/Ledger/Internal/Index" : "/Ledger/External/Index");

    public sealed class AllocationInput
    {
        public Guid SettlementId { get; set; }
        public decimal Amount { get; set; }
    }
}
