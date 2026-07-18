using System.Text.Json;
using EngineeringManager.Application.Employees;
using EngineeringManager.Application.TemporaryWorkers;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.TemporaryWorkers;

public sealed class TemporaryWorkerService(ApplicationDbContext db) : ITemporaryWorkerService
{
    public async Task<IReadOnlyList<TemporaryWorkerDto>> ListAsync(bool includeInactive, bool canViewSensitive, CancellationToken cancellationToken)
    {
        var ids = await db.TemporaryWorkers.AsNoTracking()
            .Where(item => includeInactive || item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var items = new List<TemporaryWorkerDto>(ids.Count);
        foreach (var id in ids)
        {
            items.Add((await GetAsync(id, canViewSensitive, cancellationToken))!);
        }
        return items;
    }

    public async Task<TemporaryWorkerDto?> GetAsync(Guid id, bool canViewSensitive, CancellationToken cancellationToken)
    {
        var worker = await db.TemporaryWorkers.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (worker is null) return null;
        var paymentRows = await db.PayrollPayments.AsNoTracking()
            .Where(item => item.TemporaryWorkerId == id && item.Batch.Status != PayrollBatchStatus.Voided && (item.Batch.PaymentDate.HasValue || item.PaymentDate.HasValue))
            .Select(item => new
            {
                item.PayrollBatchId,
                PayrollPaymentId = item.Id,
                item.Batch.BatchNumber,
                PaymentDate = item.Batch.PaymentDate ?? item.PaymentDate!.Value,
                item.Batch.ProjectId,
                ProjectName = item.Batch.Project != null ? item.Batch.Project.Name : null,
                item.Amount,
                item.Notes
            })
            .OrderByDescending(item => item.PaymentDate)
            .ToListAsync(cancellationToken);
        var payments = paymentRows.Select(item => new TemporaryWorkerPaymentDto(
            item.PayrollBatchId,
            item.PayrollPaymentId,
            item.BatchNumber,
            item.PaymentDate,
            item.ProjectId,
            item.ProjectName,
            item.Amount,
            item.Notes)).ToArray();
        var potentialDuplicate = await HasPotentialDuplicateAsync(worker.Id, worker.Name, worker.IdentityNumber, worker.Phone, cancellationToken);
        return new TemporaryWorkerDto(
            worker.Id,
            worker.Name,
            canViewSensitive ? worker.IdentityNumber : EmployeeSensitiveDataMasker.MaskIdentityNumber(worker.IdentityNumber),
            worker.Phone,
            canViewSensitive ? worker.BankAccountNumber : EmployeeSensitiveDataMasker.MaskBankAccountNumber(worker.BankAccountNumber),
            worker.BankName,
            worker.Trade,
            worker.DefaultProjectId,
            worker.ConvertedEmployeeId,
            worker.Notes,
            worker.IsActive,
            potentialDuplicate,
            payments.Length,
            payments.Sum(item => item.Amount),
            payments.Select(item => item.PaymentDate).Cast<DateOnly?>().FirstOrDefault(),
            worker.ConcurrencyStamp,
            payments);
    }

    public async Task<TemporaryWorkerDto> CreateAsync(string userId, CreateTemporaryWorkerRequest request, CancellationToken cancellationToken)
    {
        var name = Required(request.Name);
        await EnsureProjectAsync(request.DefaultProjectId, cancellationToken);
        var worker = new TemporaryWorker
        {
            Name = name,
            IdentityNumber = Optional(request.IdentityNumber),
            Phone = Optional(request.Phone),
            BankAccountNumber = Optional(request.BankAccountNumber),
            BankName = Optional(request.BankName),
            Trade = Optional(request.Trade),
            DefaultProjectId = request.DefaultProjectId,
            Notes = Optional(request.Notes)
        };
        db.TemporaryWorkers.Add(worker);
        AddAudit(userId, "CreateTemporaryWorker", worker.Id, Required(request.Reason), null, Snapshot(worker));
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(worker.Id, true, cancellationToken))!;
    }

    public async Task<TemporaryWorkerDto> UpdateAsync(string userId, UpdateTemporaryWorkerRequest request, CancellationToken cancellationToken)
    {
        var worker = await db.TemporaryWorkers.SingleOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("临时人员不存在。");
        if (worker.ConcurrencyStamp != request.ConcurrencyStamp) throw new DbUpdateConcurrencyException("临时人员资料已被其他用户修改，请刷新后重试。");
        await EnsureProjectAsync(request.DefaultProjectId, cancellationToken);
        var before = Snapshot(worker);
        worker.Name = Required(request.Name);
        worker.IdentityNumber = Optional(request.IdentityNumber);
        worker.Phone = Optional(request.Phone);
        worker.BankAccountNumber = Optional(request.BankAccountNumber);
        worker.BankName = Optional(request.BankName);
        worker.Trade = Optional(request.Trade);
        worker.DefaultProjectId = request.DefaultProjectId;
        worker.Notes = Optional(request.Notes);
        worker.IsActive = request.IsActive;
        worker.UpdatedAt = DateTimeOffset.UtcNow;
        worker.ConcurrencyStamp = Guid.NewGuid();
        AddAudit(userId, "UpdateTemporaryWorker", worker.Id, Required(request.Reason), before, Snapshot(worker));
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(worker.Id, true, cancellationToken))!;
    }

    public async Task LinkConvertedEmployeeAsync(string userId, Guid temporaryWorkerId, Guid employeeId, string reason, CancellationToken cancellationToken)
    {
        var worker = await db.TemporaryWorkers.SingleOrDefaultAsync(item => item.Id == temporaryWorkerId, cancellationToken)
            ?? throw new InvalidOperationException("临时人员不存在。");
        if (!await db.Employees.AnyAsync(item => item.Id == employeeId, cancellationToken)) throw new InvalidOperationException("员工不存在。");
        var before = Snapshot(worker);
        worker.ConvertedEmployeeId = employeeId;
        worker.UpdatedAt = DateTimeOffset.UtcNow;
        worker.ConcurrencyStamp = Guid.NewGuid();
        AddAudit(userId, "LinkTemporaryWorkerToEmployee", worker.Id, Required(reason), before, Snapshot(worker));
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> HasPotentialDuplicateAsync(Guid currentId, string name, string? identityNumber, string? phone, CancellationToken cancellationToken) =>
        await db.TemporaryWorkers.AnyAsync(item => item.Id != currentId &&
            (item.Name == name || (!string.IsNullOrEmpty(identityNumber) && item.IdentityNumber == identityNumber) || (!string.IsNullOrEmpty(phone) && item.Phone == phone)), cancellationToken);

    private async Task EnsureProjectAsync(Guid? projectId, CancellationToken cancellationToken)
    {
        if (projectId.HasValue && !await db.Projects.AnyAsync(item => item.Id == projectId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("默认项目不存在或已停用。");
        }
    }

    private void AddAudit(string userId, string action, Guid id, string reason, object? before, object after) => db.AuditLogs.Add(new AuditLog
    {
        UserId = userId,
        Action = action,
        EntityType = nameof(TemporaryWorker),
        EntityId = id.ToString(),
        Reason = reason,
        BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
        AfterJson = JsonSerializer.Serialize(after)
    });

    private static object Snapshot(TemporaryWorker item) => new { item.Name, item.IdentityNumber, item.Phone, item.BankAccountNumber, item.BankName, item.Trade, item.DefaultProjectId, item.ConvertedEmployeeId, item.Notes, item.IsActive };
    private static string Required(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("值不能为空。") : value.Trim();
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
