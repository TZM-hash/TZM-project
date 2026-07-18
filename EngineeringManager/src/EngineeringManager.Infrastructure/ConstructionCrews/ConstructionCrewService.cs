using System.Text.Json;
using EngineeringManager.Application.ConstructionCrews;
using EngineeringManager.Application.Employees;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.ConstructionCrews;

public sealed class ConstructionCrewService(ApplicationDbContext db) : IConstructionCrewService
{
    public async Task<IReadOnlyList<ConstructionCrewListItemDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var crews = await db.BusinessPartners.AsNoTracking()
            .Where(item => item.Roles.Any(role => role.RoleType == BusinessPartnerRoleType.ConstructionCrew) && (includeInactive || item.IsActive))
            .Include(item => item.Roles)
            .Include(item => item.Contacts)
            .Include(item => item.ProjectLinks)
            .OrderBy(item => item.PartnerNumber)
            .ToListAsync(cancellationToken);
        var crewIds = crews.Select(item => item.Id).ToArray();
        var currentCounts = await db.ConstructionCrewMemberships.AsNoTracking()
            .Where(item => crewIds.Contains(item.CrewBusinessPartnerId) && !item.EndDate.HasValue && item.Worker.IsActive)
            .GroupBy(item => item.CrewBusinessPartnerId)
            .Select(group => new { CrewId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CrewId, item => item.Count, cancellationToken);
        var payments = await db.PayrollPayments.AsNoTracking()
            .Where(item => item.CrewBusinessPartnerId.HasValue && crewIds.Contains(item.CrewBusinessPartnerId.Value) && item.Batch.Status != PayrollBatchStatus.Voided)
            .Select(item => new { CrewId = item.CrewBusinessPartnerId!.Value, Date = item.Batch.PaymentDate ?? item.PaymentDate, item.Amount })
            .ToListAsync(cancellationToken);
        return crews.Select(crew =>
        {
            var crewPayments = payments.Where(item => item.CrewId == crew.Id).ToArray();
            var contact = crew.Contacts.FirstOrDefault(item => item.IsPrimary) ?? crew.Contacts.FirstOrDefault();
            return new ConstructionCrewListItemDto(
                crew.Id,
                crew.PartnerNumber,
                crew.Name,
                crew.ShortName,
                crew.Roles.First(item => item.RoleType == BusinessPartnerRoleType.ConstructionCrew).TradeCategory,
                contact?.Name,
                contact?.Phone,
                currentCounts.GetValueOrDefault(crew.Id),
                crew.ProjectLinks.Count,
                crewPayments.Sum(item => item.Amount),
                crewPayments.Where(item => item.Date.HasValue).Select(item => item.Date!.Value).OrderByDescending(item => item).Cast<DateOnly?>().FirstOrDefault(),
                crew.IsActive);
        }).ToArray();
    }

    public async Task<ConstructionCrewDetailsDto?> GetAsync(Guid crewBusinessPartnerId, bool canViewSensitive, CancellationToken cancellationToken)
    {
        var crew = (await ListAsync(true, cancellationToken)).SingleOrDefault(item => item.Id == crewBusinessPartnerId);
        if (crew is null) return null;
        var memberships = await db.ConstructionCrewMemberships.AsNoTracking()
            .Where(item => item.CrewBusinessPartnerId == crewBusinessPartnerId)
            .Include(item => item.Worker)
            .OrderByDescending(item => !item.EndDate.HasValue)
            .ThenBy(item => item.Worker.Name)
            .ToListAsync(cancellationToken);
        var workers = memberships.Select(item => new ConstructionWorkerDto(
            item.Worker.Id,
            item.Worker.Name,
            canViewSensitive ? item.Worker.IdentityNumber : EmployeeSensitiveDataMasker.MaskIdentityNumber(item.Worker.IdentityNumber),
            item.Worker.Phone,
            canViewSensitive ? item.Worker.BankAccountNumber : EmployeeSensitiveDataMasker.MaskBankAccountNumber(item.Worker.BankAccountNumber),
            item.Worker.BankName,
            item.Worker.Trade,
            item.CrewBusinessPartnerId,
            item.StartDate,
            item.EndDate,
            item.Worker.IsActive,
            item.Worker.Notes,
            item.Worker.ConcurrencyStamp)).ToArray();
        var paymentRows = await db.PayrollPayments.AsNoTracking()
            .Where(item => item.CrewBusinessPartnerId == crewBusinessPartnerId && item.Batch.Status != PayrollBatchStatus.Voided && (item.Batch.PaymentDate.HasValue || item.PaymentDate.HasValue))
            .Select(item => new
            {
                item.PayrollBatchId,
                item.Batch.BatchNumber,
                PaymentDate = item.Batch.PaymentDate ?? item.PaymentDate!.Value,
                item.Batch.ProjectId,
                ProjectName = item.Batch.Project != null ? item.Batch.Project.Name : null,
                item.Amount,
                item.ConstructionWorkerId,
                PayrollPaymentId = item.Id,
                RecipientName = item.RecipientNameSnapshot ?? item.PayeeName,
                item.IdentityNumberSnapshot,
                item.TradeSnapshot
            })
            .ToListAsync(cancellationToken);
        var paymentBatches = paymentRows.GroupBy(item => new { item.PayrollBatchId, item.BatchNumber, item.PaymentDate, item.ProjectId, item.ProjectName })
            .Select(group => new ConstructionCrewPaymentBatchDto(
                group.Key.PayrollBatchId,
                group.Key.BatchNumber,
                group.Key.PaymentDate,
                group.Key.ProjectId,
                group.Key.ProjectName,
                group.Sum(item => item.Amount),
                group.Select(item => item.ConstructionWorkerId).Distinct().Count(),
                group.OrderBy(item => item.RecipientName).Select(item => new ConstructionCrewPaymentLineDto(
                    item.PayrollPaymentId,
                    item.ConstructionWorkerId!.Value,
                    item.RecipientName,
                    canViewSensitive ? item.IdentityNumberSnapshot : EmployeeSensitiveDataMasker.MaskIdentityNumber(item.IdentityNumberSnapshot),
                    item.TradeSnapshot,
                    item.Amount)).ToArray()))
            .OrderByDescending(item => item.PaymentDate)
            .ToArray();
        return new ConstructionCrewDetailsDto(crew, workers, paymentBatches);
    }

    public async Task<ConstructionWorkerDto> AddWorkerAsync(string userId, CreateConstructionWorkerRequest request, CancellationToken cancellationToken)
    {
        await EnsureCrewAsync(request.CrewBusinessPartnerId, cancellationToken);
        var reason = Required(request.Reason);
        var worker = new ConstructionWorker
        {
            Name = Required(request.Name),
            IdentityNumber = Optional(request.IdentityNumber),
            Phone = Optional(request.Phone),
            BankAccountNumber = Optional(request.BankAccountNumber),
            BankName = Optional(request.BankName),
            Trade = Optional(request.Trade),
            Notes = Optional(request.Notes)
        };
        worker.Memberships.Add(new ConstructionCrewMembership
        {
            Worker = worker,
            CrewBusinessPartnerId = request.CrewBusinessPartnerId,
            StartDate = request.StartDate,
            IsPrimary = true
        });
        db.ConstructionWorkers.Add(worker);
        AddAudit(userId, "CreateConstructionWorker", worker.Id, reason, null, new { worker.Name, request.CrewBusinessPartnerId, request.StartDate });
        await db.SaveChangesAsync(cancellationToken);
        return new ConstructionWorkerDto(worker.Id, worker.Name, worker.IdentityNumber, worker.Phone, worker.BankAccountNumber, worker.BankName, worker.Trade, request.CrewBusinessPartnerId, request.StartDate, null, worker.IsActive, worker.Notes, worker.ConcurrencyStamp);
    }

    public async Task TransferWorkerAsync(string userId, TransferConstructionWorkerRequest request, CancellationToken cancellationToken)
    {
        await EnsureCrewAsync(request.NewCrewBusinessPartnerId, cancellationToken);
        var worker = await db.ConstructionWorkers.Include(item => item.Memberships)
            .SingleOrDefaultAsync(item => item.Id == request.ConstructionWorkerId, cancellationToken)
            ?? throw new InvalidOperationException("班组人员不存在。");
        var current = worker.Memberships.SingleOrDefault(item => item.IsPrimary && !item.EndDate.HasValue)
            ?? throw new InvalidOperationException("班组人员没有当前主要班组归属。");
        if (request.TransferDate <= current.StartDate)
        {
            throw new InvalidOperationException("转组日期必须晚于当前班组加入日期。");
        }
        if (current.CrewBusinessPartnerId == request.NewCrewBusinessPartnerId)
        {
            throw new InvalidOperationException("新班组不能与当前班组相同。");
        }
        var reason = Required(request.Reason);
        var before = new { current.CrewBusinessPartnerId, current.StartDate, current.EndDate };
        current.EndDate = request.TransferDate.AddDays(-1);
        current.IsPrimary = false;
        var newMembership = new ConstructionCrewMembership
        {
            Worker = worker,
            CrewBusinessPartnerId = request.NewCrewBusinessPartnerId,
            StartDate = request.TransferDate,
            IsPrimary = true,
            Notes = reason
        };
        worker.Memberships.Add(newMembership);
        db.ConstructionCrewMemberships.Add(newMembership);
        worker.UpdatedAt = DateTimeOffset.UtcNow;
        worker.ConcurrencyStamp = Guid.NewGuid();
        AddAudit(userId, "TransferConstructionWorker", worker.Id, reason, before, new { request.NewCrewBusinessPartnerId, request.TransferDate });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureCrewAsync(Guid crewId, CancellationToken cancellationToken)
    {
        if (!await db.BusinessPartners.AnyAsync(item => item.Id == crewId && item.IsActive && item.Roles.Any(role => role.RoleType == BusinessPartnerRoleType.ConstructionCrew), cancellationToken))
        {
            throw new InvalidOperationException("施工班组不存在、已停用或没有施工班组角色。");
        }
    }

    private void AddAudit(string userId, string action, Guid id, string reason, object? before, object after) => db.AuditLogs.Add(new AuditLog
    {
        UserId = userId,
        Action = action,
        EntityType = nameof(ConstructionWorker),
        EntityId = id.ToString(),
        Reason = reason,
        BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
        AfterJson = JsonSerializer.Serialize(after)
    });

    private static string Required(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("值不能为空。") : value.Trim();
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
