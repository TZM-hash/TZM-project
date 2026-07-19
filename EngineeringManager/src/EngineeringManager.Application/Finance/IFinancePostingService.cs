namespace EngineeringManager.Application.Finance;

public interface IFinancePostingService
{
    Task<Guid> UpsertProjectQuantityReceivableAsync(CentralLedgerActor actor, Guid lineItemId, CancellationToken token);
    Task<Guid> CreateCrewPayableAsync(CentralLedgerActor actor, CreateCrewPayableRequest request, CancellationToken token);
    Task<Guid> CreatePartnerPayableAsync(CentralLedgerActor actor, CreatePartnerPayableRequest request, CancellationToken token);
}
