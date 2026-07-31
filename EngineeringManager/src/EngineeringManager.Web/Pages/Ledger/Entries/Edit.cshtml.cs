using EngineeringManager.Application.Finance;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Web.Pages.Ledger.Entries;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance)]
public sealed class EditModel(ICentralLedgerCommandService commands, ICentralLedgerQueryService queries, ApplicationDbContext db) : PageModel
{
    public CentralLedgerOptionsDto Options { get; private set; } = new([], [], [], [], [], [], [], [], []);
    public bool IsEditing => RecordId.HasValue && RecordId.Value != Guid.Empty;
    public bool IsReadOnly { get; private set; }
    public string? ReadOnlyReason { get; private set; }
    public LedgerSourceType CurrentSourceType { get; private set; } = LedgerSourceType.CentralLedger;

    [BindProperty(SupportsGet = true)] public LedgerScope Scope { get; set; } = LedgerScope.External;
    [BindProperty(SupportsGet = true)] public FinanceRecordType RecordType { get; set; } = FinanceRecordType.Settlement;
    [BindProperty(SupportsGet = true)] public Guid? RecordId { get; set; }
    [BindProperty(SupportsGet = true)] public string ActiveTab { get; set; } = "overview";
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

    public async Task OnGetAsync(CancellationToken token)
    {
        await LoadOptionsAsync(token);
        if (IsEditing) await LoadRecordAsync(token);
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken token)
    {
        try
        {
            var actor = await LedgerPageSupport.CreateActorAsync(User, db, token);
            if (IsEditing)
            {
                await UpdateExistingAsync(actor, token);
            }
            else
            {
                await CreateNewAsync(actor, token);
            }
            TempData["Success"] = IsEditing ? "财务记录已更新。" : "财务记录已保存到中央账本。";
            return RedirectToLedger();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException or DbUpdateConcurrencyException)
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
            TempData["Success"] = "财务记录已删除，删除日志已保留。";
            return RedirectToLedger();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadOptionsAsync(token);
            return Page();
        }
    }

    private async Task CreateNewAsync(CentralLedgerActor actor, CancellationToken token)
    {
        if (ProjectId.HasValue && ProjectId.Value != Guid.Empty && RecordType == FinanceRecordType.Settlement && Direction == LedgerDirection.Receivable)
            throw new InvalidOperationException("项目应收由工程量明细自动生成，不能手工新增。");
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
                    Notes, ToAllocations(), AutoAllocate, InvoiceType: InvoiceType, ProjectId: ProjectId, ContractId: ContractId), token);
                break;
            case FinanceRecordType.Cash:
                await commands.CreateCashAsync(actor, new CreateFinanceCashRequest(
                    Scope, Direction, Scope == LedgerScope.Internal ? LedgerCashType.InternalTransfer : Direction == LedgerDirection.Receivable ? LedgerCashType.Collection : LedgerCashType.Payment,
                    LedgerSourceType.CentralLedger, null, LegalEntityId, BusinessPartnerId, CounterLegalEntityId, AccountId,
                    CounterAccountId, BusinessDate, Amount, PaymentMethod, Notes, ToAllocations(), AutoAllocate, ProjectId, ContractId), token);
                break;
            default:
                throw new ArgumentException("当前页面不支持该财务记录类型。");
        }
    }

    private async Task UpdateExistingAsync(CentralLedgerActor actor, CancellationToken token)
    {
        if (IsReadOnly) throw new InvalidOperationException(ReadOnlyReason ?? "当前记录不可直接编辑。");
        var id = RecordId!.Value;
        switch (RecordType)
        {
            case FinanceRecordType.Settlement:
                await commands.UpdateSettlementAsync(actor, new UpdateSettlementRequest(
                    id, Scope, Direction, SettlementState, LegalEntityId, BusinessPartnerId, CounterLegalEntityId,
                    ProjectId, ContractId, BusinessDate, Amount, InvoiceAmount == 0m ? Amount : InvoiceAmount,
                    Notes, Reason, SettlementConcurrencyStamp), token);
                break;
            case FinanceRecordType.Invoice:
                await commands.UpdateInvoiceAsync(actor, new UpdateFinanceInvoiceRequest(
                    id, Scope, Direction, LegalEntityId, BusinessPartnerId, CounterLegalEntityId, ProjectId, ContractId,
                    InvoiceNumber ?? string.Empty, BusinessDate, Amount, NetAmount, TaxAmount, TaxRate, InvoiceType, Notes,
                    Reason, SettlementConcurrencyStamp), token);
                break;
            case FinanceRecordType.Cash:
                await commands.UpdateCashAsync(actor, new UpdateFinanceCashRequest(
                    id, Scope, Direction, Scope == LedgerScope.Internal ? LedgerCashType.InternalTransfer : Direction == LedgerDirection.Receivable ? LedgerCashType.Collection : LedgerCashType.Payment,
                    LegalEntityId, BusinessPartnerId, CounterLegalEntityId, ProjectId, ContractId, AccountId, CounterAccountId,
                    BusinessDate, Amount, PaymentMethod, Notes, Reason, SettlementConcurrencyStamp), token);
                break;
            default:
                throw new InvalidOperationException("扣款和调整记录请通过关联结算的专用操作处理。");
        }
    }

    private async Task LoadRecordAsync(CancellationToken token)
    {
        switch (RecordType)
        {
            case FinanceRecordType.Settlement:
                var settlement = await db.FinanceSettlements.AsNoTracking().Include(item => item.InvoiceAllocations).Include(item => item.CashAllocations).SingleOrDefaultAsync(item => item.Id == RecordId, token) ?? throw new KeyNotFoundException("中央账本结算记录不存在。");
                Scope = settlement.Scope; Direction = settlement.Direction; SettlementState = settlement.SettlementState; LegalEntityId = settlement.LegalEntityId; BusinessPartnerId = settlement.BusinessPartnerId; CounterLegalEntityId = settlement.CounterLegalEntityId; ProjectId = settlement.ProjectId; ContractId = settlement.ContractId; BusinessDate = settlement.BusinessDate; Amount = settlement.OriginalAmount; InvoiceAmount = settlement.OriginalInvoiceAmount; Notes = settlement.Notes; SettlementConcurrencyStamp = settlement.ConcurrencyStamp; CurrentSourceType = settlement.SourceType; IsReadOnly = settlement.SourceType != LedgerSourceType.CentralLedger || settlement.InvoiceAllocations.Count + settlement.CashAllocations.Count > 0; ReadOnlyReason = settlement.SourceType != LedgerSourceType.CentralLedger ? "该结算由来源模块生成，请返回来源模块修改。" : "该结算已发生分摊，请通过调整或冲销修改。"; break;
            case FinanceRecordType.Invoice:
                var invoice = await db.FinanceInvoices.AsNoTracking().Include(item => item.Allocations).SingleOrDefaultAsync(item => item.Id == RecordId, token) ?? throw new KeyNotFoundException("发票记录不存在。");
                Scope = invoice.Scope; Direction = invoice.Direction; LegalEntityId = invoice.LegalEntityId; BusinessPartnerId = invoice.BusinessPartnerId; CounterLegalEntityId = invoice.CounterLegalEntityId; ProjectId = invoice.ProjectId; ContractId = invoice.ContractId; BusinessDate = invoice.InvoiceDate; Amount = invoice.Amount; InvoiceNumber = invoice.InvoiceNumber; InvoiceType = invoice.InvoiceType; NetAmount = invoice.NetAmount; TaxAmount = invoice.TaxAmount; TaxRate = invoice.TaxRate; Notes = invoice.Notes; SettlementConcurrencyStamp = invoice.ConcurrencyStamp; CurrentSourceType = invoice.SourceType; IsReadOnly = invoice.SourceType != LedgerSourceType.CentralLedger || invoice.Allocations.Count > 0; ReadOnlyReason = invoice.SourceType != LedgerSourceType.CentralLedger ? "该发票由来源模块生成，请返回来源模块修改。" : "该发票已发生分摊，请先进入分摊调整。"; break;
            case FinanceRecordType.Cash:
                var cash = await db.FinanceCashEntries.AsNoTracking().Include(item => item.Allocations).SingleOrDefaultAsync(item => item.Id == RecordId, token) ?? throw new KeyNotFoundException("资金记录不存在。");
                Scope = cash.Scope; Direction = cash.Direction; LegalEntityId = cash.LegalEntityId; BusinessPartnerId = cash.BusinessPartnerId; CounterLegalEntityId = cash.CounterLegalEntityId; ProjectId = cash.ProjectId; ContractId = cash.ContractId; BusinessDate = cash.BusinessDate; Amount = cash.Amount; AccountId = cash.AccountId; CounterAccountId = cash.CounterAccountId; PaymentMethod = cash.PaymentMethod; Notes = cash.Notes; SettlementConcurrencyStamp = cash.ConcurrencyStamp; CurrentSourceType = cash.SourceType; IsReadOnly = cash.SourceType != LedgerSourceType.CentralLedger || cash.Allocations.Count > 0; ReadOnlyReason = cash.SourceType != LedgerSourceType.CentralLedger ? "该资金记录由来源模块生成，请返回来源模块修改。" : "该资金记录已发生分摊，请先进入分摊调整。"; break;
            default:
                IsReadOnly = true; ReadOnlyReason = "该记录类型不支持直接编辑。"; break;
        }
        await LoadOptionsAsync(token);
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

    private RedirectToPageResult RedirectToLedger() => RedirectToPage(Scope == LedgerScope.Internal ? "/Ledger/Internal/Index" : "/Ledger/External/Index", new { view = ActiveTab });

    public sealed class AllocationInput
    {
        public Guid SettlementId { get; set; }
        public decimal Amount { get; set; }
    }
}
