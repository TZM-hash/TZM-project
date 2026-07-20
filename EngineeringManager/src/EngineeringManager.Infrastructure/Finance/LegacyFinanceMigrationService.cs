using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Finance;

public sealed record LegacyFinanceMigrationOptions(string? PreflightReportPath = null);

public sealed record LegacyFinanceMigrationIssue(string LegacyEntityType, Guid LegacyId, string Message);

public sealed record LegacyQuantityConflict(
    [property: JsonPropertyName("contract_line_item_id")] Guid ContractLineItemId,
    [property: JsonPropertyName("project_id")] Guid ProjectId,
    [property: JsonPropertyName("contract_id")] Guid ContractId,
    [property: JsonPropertyName("quantity_current_amount")] decimal QuantityCurrentAmount,
    [property: JsonPropertyName("candidate_legacy_receivable_ids")] IReadOnlyList<Guid> CandidateLegacyReceivableIds,
    [property: JsonPropertyName("candidate_amounts")] IReadOnlyList<decimal> CandidateAmounts,
    [property: JsonPropertyName("match_reason")] string MatchReason,
    [property: JsonPropertyName("resolution")] string Resolution);

public sealed record LegacyFinanceMigrationResult(
    bool CanApply,
    IReadOnlyList<LegacyFinanceMigrationIssue> Exceptions,
    IReadOnlyList<LegacyQuantityConflict> QuantityConflicts);

public sealed class LegacyFinanceMigrationService(ApplicationDbContext db)
{
    private static readonly JsonSerializerOptions ReportJsonOptions = new() { WriteIndented = true };

    public async Task<LegacyFinanceMigrationResult> MigrateAsync(LegacyFinanceMigrationOptions options, CancellationToken token)
    {
        var exceptions = new List<LegacyFinanceMigrationIssue>();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var maps = await db.FinanceLegacyMaps.ToDictionaryAsync(item => MapKey(item.LegacyEntityType, Guid.Parse(item.LegacyId)), token);

        var receivables = await db.ReceivableEntries.AsNoTracking().OrderBy(item => item.Id).ToListAsync(token);
        foreach (var legacy in receivables)
        {
            if (maps.ContainsKey(MapKey(nameof(ReceivableEntry), legacy.Id))) continue;
            if (!legacy.BusinessPartnerId.HasValue)
            {
                exceptions.Add(new LegacyFinanceMigrationIssue(nameof(ReceivableEntry), legacy.Id, "旧应收缺少合作单位，保留待人工处理。"));
                continue;
            }
            var id = DeterministicGuid($"central:settlement:receivable:{legacy.Id:D}");
            db.FinanceSettlements.Add(new FinanceSettlement
            {
                Id = id,
                Scope = LedgerScope.External,
                Direction = LedgerDirection.Receivable,
                SettlementState = LedgerSettlementState.Final,
                SourceType = LedgerSourceType.LegacyMigration,
                SourceId = DeterministicGuid($"source:receivable:{legacy.Id:D}"),
                ProjectId = legacy.ProjectId,
                ContractId = legacy.ContractId,
                LegalEntityId = legacy.LegalEntityId,
                BusinessPartnerId = legacy.BusinessPartnerId,
                BusinessDate = legacy.EntryDate,
                OriginalAmount = legacy.Amount,
                OriginalInvoiceAmount = legacy.Amount,
                Notes = legacy.Description,
                Status = legacy.IsVoided ? LedgerRecordStatus.Voided : LedgerRecordStatus.Active
            });
            AddMap(maps, nameof(ReceivableEntry), legacy.Id, FinanceRecordType.Settlement, id);
        }

        var payables = await db.PayableEntries.AsNoTracking().OrderBy(item => item.Id).ToListAsync(token);
        foreach (var legacy in payables)
        {
            if (maps.ContainsKey(MapKey(nameof(PayableEntry), legacy.Id))) continue;
            var id = DeterministicGuid($"central:settlement:payable:{legacy.Id:D}");
            db.FinanceSettlements.Add(new FinanceSettlement
            {
                Id = id,
                Scope = LedgerScope.External,
                Direction = LedgerDirection.Payable,
                SettlementState = LedgerSettlementState.Final,
                SourceType = LedgerSourceType.LegacyMigration,
                SourceId = DeterministicGuid($"source:payable:{legacy.Id:D}"),
                ProjectId = legacy.ProjectId,
                ContractId = legacy.ContractId,
                LegalEntityId = legacy.LegalEntityId,
                BusinessPartnerId = legacy.BusinessPartnerId,
                BusinessDate = legacy.EntryDate,
                OriginalAmount = legacy.Amount,
                OriginalInvoiceAmount = legacy.Amount,
                Notes = legacy.Description,
                Status = legacy.IsVoided ? LedgerRecordStatus.Voided : LedgerRecordStatus.Active
            });
            AddMap(maps, nameof(PayableEntry), legacy.Id, FinanceRecordType.Settlement, id);
        }

        var collections = await db.CollectionEntries.AsNoTracking().OrderBy(item => item.Id).ToListAsync(token);
        foreach (var legacy in collections)
        {
            if (maps.ContainsKey(MapKey(nameof(CollectionEntry), legacy.Id))) continue;
            var id = DeterministicGuid($"central:cash:collection:{legacy.Id:D}");
            var cash = new FinanceCashEntry
            {
                Id = id,
                Scope = LedgerScope.External,
                Direction = LedgerDirection.Receivable,
                CashType = LedgerCashType.Collection,
                LegalEntityId = legacy.LegalEntityId,
                BusinessPartnerId = legacy.BusinessPartnerId,
                AccountId = legacy.AccountId,
                BusinessDate = legacy.CollectionDate,
                Amount = legacy.Amount,
                PaymentMethod = legacy.PaymentMethod.ToString(),
                SourceType = LedgerSourceType.LegacyMigration,
                SourceId = DeterministicGuid($"source:collection:{legacy.Id:D}"),
                Notes = legacy.Notes
            };
            if (legacy.ReceivableEntryId.HasValue && TryCentralId(maps, nameof(ReceivableEntry), legacy.ReceivableEntryId.Value, out var settlementId))
            {
                cash.Allocations.Add(new FinanceCashAllocation
                {
                    Id = DeterministicGuid($"central:cash-allocation:collection:{legacy.Id:D}"),
                    SettlementId = settlementId,
                    ProjectId = legacy.ProjectId,
                    ContractId = legacy.ContractId,
                    BusinessPartnerId = legacy.BusinessPartnerId,
                    Amount = legacy.Amount,
                    AllocationOrder = 1
                });
            }
            db.FinanceCashEntries.Add(cash);
            AddMap(maps, nameof(CollectionEntry), legacy.Id, FinanceRecordType.Cash, id);
        }

        var payments = await db.PaymentEntries.AsNoTracking().OrderBy(item => item.Id).ToListAsync(token);
        foreach (var legacy in payments)
        {
            if (maps.ContainsKey(MapKey(nameof(PaymentEntry), legacy.Id))) continue;
            var id = DeterministicGuid($"central:cash:payment:{legacy.Id:D}");
            var cash = new FinanceCashEntry
            {
                Id = id,
                Scope = LedgerScope.External,
                Direction = LedgerDirection.Payable,
                CashType = LedgerCashType.Payment,
                LegalEntityId = legacy.LegalEntityId,
                BusinessPartnerId = legacy.BusinessPartnerId,
                AccountId = legacy.AccountId,
                BusinessDate = legacy.PaymentDate,
                Amount = legacy.Amount,
                PaymentMethod = legacy.PaymentMethod.ToString(),
                SourceType = LedgerSourceType.LegacyMigration,
                SourceId = DeterministicGuid($"source:payment:{legacy.Id:D}"),
                Notes = legacy.Notes
            };
            if (legacy.PayableEntryId.HasValue && TryCentralId(maps, nameof(PayableEntry), legacy.PayableEntryId.Value, out var settlementId))
            {
                cash.Allocations.Add(new FinanceCashAllocation
                {
                    Id = DeterministicGuid($"central:cash-allocation:payment:{legacy.Id:D}"),
                    SettlementId = settlementId,
                    ProjectId = legacy.ProjectId,
                    ContractId = legacy.ContractId,
                    BusinessPartnerId = legacy.BusinessPartnerId,
                    Amount = legacy.Amount,
                    AllocationOrder = 1
                });
            }
            db.FinanceCashEntries.Add(cash);
            AddMap(maps, nameof(PaymentEntry), legacy.Id, FinanceRecordType.Cash, id);
        }

        foreach (var legacy in await db.RefundOrReversalEntries.AsNoTracking().OrderBy(item => item.Id).ToListAsync(token))
        {
            if (maps.ContainsKey(MapKey(nameof(RefundOrReversalEntry), legacy.Id))) continue;
            var sourceCollection = legacy.CollectionEntryId.HasValue ? collections.SingleOrDefault(item => item.Id == legacy.CollectionEntryId.Value) : null;
            var sourceReceivableId = legacy.ReceivableEntryId ?? sourceCollection?.ReceivableEntryId;
            var sourceReceivable = sourceReceivableId.HasValue ? receivables.SingleOrDefault(item => item.Id == sourceReceivableId.Value) : null;
            if (sourceCollection is null || sourceReceivable is null || !sourceReceivable.BusinessPartnerId.HasValue
                || !TryCentralId(maps, nameof(CollectionEntry), sourceCollection.Id, out var reversedCashId)
                || !TryCentralId(maps, nameof(ReceivableEntry), sourceReceivable.Id, out var settlementId))
            {
                exceptions.Add(new LegacyFinanceMigrationIssue(nameof(RefundOrReversalEntry), legacy.Id, "旧退款或冲销找不到对应的收款和应收记录。"));
                continue;
            }
            var id = DeterministicGuid($"central:cash:collection-reversal:{legacy.Id:D}");
            var cash = new FinanceCashEntry
            {
                Id = id,
                Scope = LedgerScope.External,
                Direction = LedgerDirection.Receivable,
                CashType = LedgerCashType.Collection,
                LegalEntityId = sourceReceivable.LegalEntityId,
                BusinessPartnerId = sourceReceivable.BusinessPartnerId,
                AccountId = legacy.AccountId,
                IsReversal = true,
                ReversesCashEntryId = reversedCashId,
                BusinessDate = legacy.EntryDate,
                Amount = legacy.Amount,
                SourceType = LedgerSourceType.LegacyMigration,
                SourceId = DeterministicGuid($"source:collection-reversal:{legacy.Id:D}"),
                Notes = legacy.Reason
            };
            cash.Allocations.Add(new FinanceCashAllocation
            {
                Id = DeterministicGuid($"central:cash-allocation:collection-reversal:{legacy.Id:D}"),
                SettlementId = settlementId,
                ProjectId = sourceReceivable.ProjectId,
                ContractId = sourceReceivable.ContractId,
                BusinessPartnerId = sourceReceivable.BusinessPartnerId,
                Amount = legacy.Amount,
                AllocationOrder = 1
            });
            db.FinanceCashEntries.Add(cash);
            AddMap(maps, nameof(RefundOrReversalEntry), legacy.Id, FinanceRecordType.Cash, id);
        }

        foreach (var legacy in await db.PaymentReversalEntries.AsNoTracking().OrderBy(item => item.Id).ToListAsync(token))
        {
            if (maps.ContainsKey(MapKey(nameof(PaymentReversalEntry), legacy.Id))) continue;
            var sourcePayment = payments.SingleOrDefault(item => item.Id == legacy.PaymentEntryId);
            var sourcePayable = sourcePayment?.PayableEntryId is Guid payableId ? payables.SingleOrDefault(item => item.Id == payableId) : null;
            if (sourcePayment is null || sourcePayable is null
                || !TryCentralId(maps, nameof(PaymentEntry), sourcePayment.Id, out var reversedCashId)
                || !TryCentralId(maps, nameof(PayableEntry), sourcePayable.Id, out var settlementId))
            {
                exceptions.Add(new LegacyFinanceMigrationIssue(nameof(PaymentReversalEntry), legacy.Id, "旧付款冲销找不到对应的付款和应付记录。"));
                continue;
            }
            var id = DeterministicGuid($"central:cash:payment-reversal:{legacy.Id:D}");
            var cash = new FinanceCashEntry
            {
                Id = id,
                Scope = LedgerScope.External,
                Direction = LedgerDirection.Payable,
                CashType = LedgerCashType.Payment,
                LegalEntityId = sourcePayable.LegalEntityId,
                BusinessPartnerId = sourcePayable.BusinessPartnerId,
                AccountId = legacy.AccountId,
                IsReversal = true,
                ReversesCashEntryId = reversedCashId,
                BusinessDate = legacy.EntryDate,
                Amount = legacy.Amount,
                SourceType = LedgerSourceType.LegacyMigration,
                SourceId = DeterministicGuid($"source:payment-reversal:{legacy.Id:D}"),
                Notes = legacy.Reason
            };
            cash.Allocations.Add(new FinanceCashAllocation
            {
                Id = DeterministicGuid($"central:cash-allocation:payment-reversal:{legacy.Id:D}"),
                SettlementId = settlementId,
                ProjectId = sourcePayable.ProjectId,
                ContractId = sourcePayable.ContractId,
                BusinessPartnerId = sourcePayable.BusinessPartnerId,
                Amount = legacy.Amount,
                AllocationOrder = 1
            });
            db.FinanceCashEntries.Add(cash);
            AddMap(maps, nameof(PaymentReversalEntry), legacy.Id, FinanceRecordType.Cash, id);
        }

        var transfers = await db.AccountTransfers.AsNoTracking()
            .Include(item => item.FromAccount)
            .Include(item => item.ToAccount)
            .OrderBy(item => item.Id)
            .ToListAsync(token);
        foreach (var legacy in transfers)
        {
            if (maps.ContainsKey(MapKey(nameof(AccountTransfer), legacy.Id))) continue;
            if (legacy.FromAccount.LegalEntityId == legacy.ToAccount.LegalEntityId)
            {
                exceptions.Add(new LegacyFinanceMigrationIssue(nameof(AccountTransfer), legacy.Id, "同一自有公司的账户调拨继续保留在账户流水，不生成内部往来账。"));
                continue;
            }
            var id = DeterministicGuid($"central:cash:internal-transfer:{legacy.Id:D}");
            db.FinanceCashEntries.Add(new FinanceCashEntry
            {
                Id = id,
                Scope = LedgerScope.Internal,
                Direction = LedgerDirection.Payable,
                CashType = LedgerCashType.InternalTransfer,
                LegalEntityId = legacy.FromAccount.LegalEntityId,
                CounterLegalEntityId = legacy.ToAccount.LegalEntityId,
                AccountId = legacy.FromAccountId,
                CounterAccountId = legacy.ToAccountId,
                BusinessDate = legacy.TransferDate,
                Amount = legacy.Amount,
                SourceType = LedgerSourceType.LegacyMigration,
                SourceId = DeterministicGuid($"source:internal-transfer:{legacy.Id:D}"),
                Notes = legacy.Description
            });
            AddMap(maps, nameof(AccountTransfer), legacy.Id, FinanceRecordType.Cash, id);
        }

        foreach (var legacy in await db.DeductionEntries.AsNoTracking().OrderBy(item => item.Id).ToListAsync(token))
        {
            if (maps.ContainsKey(MapKey(nameof(DeductionEntry), legacy.Id))) continue;
            if (!TryCentralId(maps, nameof(PayableEntry), legacy.PayableEntryId, out var settlementId))
            {
                exceptions.Add(new LegacyFinanceMigrationIssue(nameof(DeductionEntry), legacy.Id, "旧扣款找不到对应的应付记录。"));
                continue;
            }
            var id = DeterministicGuid($"central:deduction:{legacy.Id:D}");
            db.FinanceDeductions.Add(new FinanceDeduction
            {
                Id = id,
                SettlementId = settlementId,
                BusinessDate = legacy.EntryDate,
                Amount = legacy.Amount,
                ReduceInvoiceAmount = false,
                Reason = legacy.Reason,
                SourceType = LedgerSourceType.LegacyMigration,
                SourceId = DeterministicGuid($"source:deduction:{legacy.Id:D}")
            });
            AddMap(maps, nameof(DeductionEntry), legacy.Id, FinanceRecordType.Deduction, id);
        }

        var invoices = await db.InvoiceEntries.AsNoTracking()
            .Include(item => item.ReceivableLinks)
            .OrderBy(item => item.Id)
            .ToListAsync(token);
        foreach (var legacy in invoices)
        {
            if (maps.ContainsKey(MapKey(nameof(InvoiceEntry), legacy.Id))) continue;
            if (!legacy.BusinessPartnerId.HasValue)
            {
                exceptions.Add(new LegacyFinanceMigrationIssue(nameof(InvoiceEntry), legacy.Id, "旧发票缺少合作单位，保留待人工处理。"));
                continue;
            }
            var id = DeterministicGuid($"central:invoice:{legacy.Id:D}");
            var invoice = new FinanceInvoice
            {
                Id = id,
                Scope = LedgerScope.External,
                Direction = legacy.Direction == InvoiceDirection.Output ? LedgerDirection.Receivable : LedgerDirection.Payable,
                LegalEntityId = legacy.LegalEntityId,
                BusinessPartnerId = legacy.BusinessPartnerId,
                InvoiceNumber = legacy.InvoiceNumber,
                InvoiceDate = legacy.InvoiceDate,
                ProjectTaxConfigurationId = legacy.ProjectTaxConfigurationId,
                InvoiceType = legacy.InvoiceType,
                TaxRate = legacy.TaxRate,
                NetAmount = legacy.NetAmount,
                TaxAmount = legacy.TaxAmount,
                Amount = legacy.GrossAmount,
                Status = legacy.Status == InvoiceStatus.Voided ? LedgerRecordStatus.Voided : LedgerRecordStatus.Active,
                SourceType = LedgerSourceType.LegacyMigration,
                SourceId = DeterministicGuid($"source:invoice:{legacy.Id:D}")
            };
            var order = 1;
            foreach (var link in legacy.ReceivableLinks.OrderBy(item => item.Id))
            {
                if (!TryCentralId(maps, nameof(ReceivableEntry), link.ReceivableEntryId, out var settlementId)) continue;
                var source = receivables.Single(item => item.Id == link.ReceivableEntryId);
                invoice.Allocations.Add(new FinanceInvoiceAllocation
                {
                    Id = DeterministicGuid($"central:invoice-allocation:{link.Id:D}"),
                    SettlementId = settlementId,
                    ProjectId = source.ProjectId,
                    ContractId = source.ContractId,
                    BusinessPartnerId = source.BusinessPartnerId,
                    Amount = link.AllocatedAmount,
                    AllocationOrder = order++
                });
            }
            db.FinanceInvoices.Add(invoice);
            AddMap(maps, nameof(InvoiceEntry), legacy.Id, FinanceRecordType.Invoice, id);
        }

        await db.SaveChangesAsync(token);
        var conflicts = await BuildQuantityConflictsAsync(receivables, token);
        if (!string.IsNullOrWhiteSpace(options.PreflightReportPath))
        {
            var fullPath = Path.GetFullPath(options.PreflightReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, JsonSerializer.Serialize(conflicts, ReportJsonOptions), Encoding.UTF8, token);
        }
        await transaction.CommitAsync(token);
        return new LegacyFinanceMigrationResult(
            exceptions.Count == 0 && conflicts.All(item => item.Resolution != "manual-review"),
            exceptions,
            conflicts);
    }

    private async Task<IReadOnlyList<LegacyQuantityConflict>> BuildQuantityConflictsAsync(IReadOnlyList<ReceivableEntry> receivables, CancellationToken token)
    {
        var lines = await db.ContractLineItems.AsNoTracking().Include(item => item.Contract).OrderBy(item => item.Id).ToListAsync(token);
        var conflicts = new List<LegacyQuantityConflict>();
        foreach (var line in lines)
        {
            var amount = (line.Quantity ?? 0m) * (line.UnitPrice ?? 0m);
            if (amount <= 0m) continue;
            var candidates = receivables.Where(item => item.ProjectId == line.Contract.ProjectId && item.ContractId == line.ContractId).ToArray();
            var exact = candidates.Where(item => Math.Abs(item.Amount - amount) <= 0.01m).ToArray();
            var approximate = candidates.Where(item => Math.Abs(item.Amount - amount) <= Math.Max(1m, amount * 0.01m)).ToArray();
            var matched = exact.Length > 0 ? exact : approximate;
            var reason = exact.Length > 0 ? "exact-amount" : approximate.Length > 0 ? "approximate-amount" : "no-candidate";
            conflicts.Add(new LegacyQuantityConflict(
                line.Id,
                line.Contract.ProjectId,
                line.ContractId,
                amount,
                matched.Select(item => item.Id).ToArray(),
                matched.Select(item => item.Amount).ToArray(),
                reason,
                "manual-review"));
        }
        return conflicts;
    }

    private void AddMap(Dictionary<string, FinanceLegacyMap> maps, string legacyType, Guid legacyId, FinanceRecordType recordType, Guid centralId)
    {
        var map = new FinanceLegacyMap
        {
            Id = DeterministicGuid($"central:legacy-map:{legacyType}:{legacyId:D}"),
            LegacyEntityType = legacyType,
            LegacyId = legacyId.ToString("D"),
            CentralRecordType = recordType,
            CentralRecordId = centralId
        };
        db.FinanceLegacyMaps.Add(map);
        maps.Add(MapKey(legacyType, legacyId), map);
    }

    private static bool TryCentralId(Dictionary<string, FinanceLegacyMap> maps, string legacyType, Guid legacyId, out Guid centralId)
    {
        if (maps.TryGetValue(MapKey(legacyType, legacyId), out var map))
        {
            centralId = map.CentralRecordId;
            return true;
        }
        centralId = Guid.Empty;
        return false;
    }

    private static string MapKey(string legacyType, Guid legacyId) => $"{legacyType}|{legacyId:D}";

    private static Guid DeterministicGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
