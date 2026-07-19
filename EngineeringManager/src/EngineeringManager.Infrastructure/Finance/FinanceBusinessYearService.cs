using System.Text.Json;
using EngineeringManager.Application.Finance;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Finance;

public sealed class FinanceBusinessYearService(ApplicationDbContext db) : IFinanceBusinessYearService
{
    public async Task<IReadOnlyList<FinanceBusinessYearDto>> ListAsync(CancellationToken token)
    {
        var years = await db.FinanceBusinessYears.AsNoTracking().OrderByDescending(item => item.StartDate).ToListAsync(token);
        var result = new List<FinanceBusinessYearDto>(years.Count);
        foreach (var year in years)
        {
            var settlementCount = await db.FinanceSettlements.CountAsync(item => item.BusinessDate >= year.StartDate && item.BusinessDate <= year.EndDate, token);
            var cashCount = await db.FinanceCashEntries.CountAsync(item => item.BusinessDate >= year.StartDate && item.BusinessDate <= year.EndDate, token);
            var invoiceCount = await db.FinanceInvoices.CountAsync(item => item.InvoiceDate >= year.StartDate && item.InvoiceDate <= year.EndDate, token);
            var deductionCount = await db.FinanceDeductions.CountAsync(item => item.BusinessDate >= year.StartDate && item.BusinessDate <= year.EndDate, token);
            var adjustmentCount = await db.FinanceSettlementAdjustments.CountAsync(item => item.BusinessDate >= year.StartDate && item.BusinessDate <= year.EndDate, token);
            result.Add(ToDto(year, settlementCount + cashCount + invoiceCount + deductionCount + adjustmentCount));
        }
        return result;
    }

    public async Task<FinanceBusinessYearDto> CreateAsync(CentralLedgerActor actor, CreateFinanceBusinessYearRequest request, CancellationToken token)
    {
        EnsureCanManage(actor);
        var name = string.IsNullOrWhiteSpace(request.Name) ? throw new ArgumentException("财务年度名称不能为空。", nameof(request)) : request.Name.Trim();
        if (request.StartDate > request.EndDate) throw new ArgumentException("财务年度开始日期不能晚于结束日期。", nameof(request));
        if (await db.FinanceBusinessYears.AnyAsync(item => item.StartDate <= request.EndDate && item.EndDate >= request.StartDate, token))
            throw new InvalidOperationException("财务业务年度日期范围不能重叠。");
        var year = new FinanceBusinessYear
        {
            Name = name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CreatedByUserId = actor.UserId
        };
        db.FinanceBusinessYears.Add(year);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = actor.UserId,
            UserName = actor.UserName,
            Action = "Create",
            EntityType = nameof(FinanceBusinessYear),
            EntityId = year.Id.ToString("D"),
            AfterJson = JsonSerializer.Serialize(new { year.Name, year.StartDate, year.EndDate })
        });
        await db.SaveChangesAsync(token);
        return ToDto(year, 0);
    }

    public async Task<FinanceBusinessYearDto?> ResolveAsync(DateOnly businessDate, CancellationToken token)
    {
        var year = await db.FinanceBusinessYears.AsNoTracking()
            .SingleOrDefaultAsync(item => item.StartDate <= businessDate && item.EndDate >= businessDate, token);
        return year is null ? null : ToDto(year, 0);
    }

    public async Task DeleteAsync(CentralLedgerActor actor, Guid id, Guid concurrencyStamp, string reason, CancellationToken token)
    {
        EnsureCanManage(actor);
        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? throw new ArgumentException("删除原因不能为空。", nameof(reason)) : reason.Trim();
        var year = await db.FinanceBusinessYears.SingleOrDefaultAsync(item => item.Id == id, token)
            ?? throw new KeyNotFoundException("财务业务年度不存在。");
        if (year.ConcurrencyStamp != concurrencyStamp) throw new DbUpdateConcurrencyException("财务业务年度已被其他用户修改，请刷新后重试。");
        var before = JsonSerializer.Serialize(new { year.Name, year.StartDate, year.EndDate, year.ConcurrencyStamp });
        db.FinanceBusinessYears.Remove(year);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = actor.UserId,
            UserName = actor.UserName,
            Action = "Delete",
            EntityType = nameof(FinanceBusinessYear),
            EntityId = year.Id.ToString("D"),
            Reason = normalizedReason,
            BeforeJson = before
        });
        await db.SaveChangesAsync(token);
    }

    private static FinanceBusinessYearDto ToDto(FinanceBusinessYear year, int recordCount) =>
        new(year.Id, year.Name, year.StartDate, year.EndDate, recordCount, year.ConcurrencyStamp);

    private static void EnsureCanManage(CentralLedgerActor actor)
    {
        if (!actor.CanManageYears) throw new UnauthorizedAccessException("没有财务业务年度管理权限。");
    }
}
