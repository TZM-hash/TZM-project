using System.Text.Json;
using EngineeringManager.Application.Certificates;
using EngineeringManager.Domain.Certificates;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Files;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Certificates;

public sealed class EmployeeCertificateService(ApplicationDbContext db, IFileStore fileStore) : IEmployeeCertificateService
{
    public async Task<IReadOnlyList<EmployeeCertificateDto>> ListAsync(CertificateFilter filter, DateOnly today, CancellationToken cancellationToken)
    {
        var query = db.EmployeeCertificates.AsNoTracking().Include(item => item.Employee).Include(item => item.Attachment).Where(item => !item.IsDeleted);
        if (filter.OwnerId.HasValue) query = query.Where(item => item.EmployeeId == filter.OwnerId);
        if (!string.IsNullOrWhiteSpace(filter.CertificateType)) query = query.Where(item => item.CertificateType == filter.CertificateType.Trim());
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(item => item.Employee.EmployeeNumber.Contains(term) || item.Employee.Name.Contains(term) || item.CertificateType.Contains(term) || (item.CertificateNumber != null && item.CertificateNumber.Contains(term)));
        }
        var items = await query.OrderBy(item => item.ExpiresOn == null).ThenBy(item => item.ExpiresOn).ThenBy(item => item.Employee.EmployeeNumber).ToListAsync(cancellationToken);
        var result = items.Select(item => ToDto(item, today)).ToArray();
        return filter.State.HasValue ? result.Where(item => item.State == filter.State).ToArray() : result;
    }

    public async Task<EmployeeCertificateDto> GetAsync(Guid id, DateOnly today, CancellationToken cancellationToken)
    {
        var item = await Query().SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken) ?? throw new KeyNotFoundException("员工证书不存在。");
        return ToDto(item, today);
    }

    public async Task<EmployeeCertificateDto> SaveAsync(string userId, bool canManage, SaveEmployeeCertificateRequest request, DateOnly today, CancellationToken cancellationToken)
    {
        EnsureManage(canManage);
        var reason = CertificateServiceSupport.Required(request.Reason, nameof(request.Reason));
        CertificateServiceSupport.ValidateDates(request.IssuedOn, request.ExpiresOn, nameof(request));
        if (!await db.Employees.AnyAsync(item => item.Id == request.EmployeeId, cancellationToken)) throw new InvalidOperationException("员工不存在。");
        EmployeeCertificate item;
        string? before = null;
        if (request.Id.HasValue)
        {
            item = await db.EmployeeCertificates.Include(value => value.Attachment).SingleOrDefaultAsync(value => value.Id == request.Id && !value.IsDeleted, cancellationToken) ?? throw new KeyNotFoundException("员工证书不存在。");
            if (!request.ConcurrencyStamp.HasValue || request.ConcurrencyStamp != item.ConcurrencyStamp) throw new DbUpdateConcurrencyException("员工证书已被其他用户修改。");
            before = JsonSerializer.Serialize(Snapshot(item));
        }
        else
        {
            item = new EmployeeCertificate();
            db.EmployeeCertificates.Add(item);
        }
        if (request.NewAttachment is not null)
        {
            await CertificateServiceSupport.RemoveAttachmentAsync(item.Attachment, fileStore, cancellationToken);
            item.Attachment = await CertificateServiceSupport.SaveAttachmentAsync(db, fileStore, request.NewAttachment, userId, cancellationToken);
        }
        else if (request.RemoveAttachment)
        {
            await CertificateServiceSupport.RemoveAttachmentAsync(item.Attachment, fileStore, cancellationToken);
            item.AttachmentId = null;
            item.Attachment = null;
        }
        item.EmployeeId = request.EmployeeId;
        item.CertificateType = CertificateServiceSupport.Required(request.CertificateType, nameof(request.CertificateType));
        item.CertificateNumber = CertificateServiceSupport.Optional(request.CertificateNumber);
        item.SpecialtyLevelScope = CertificateServiceSupport.Optional(request.SpecialtyLevelScope);
        item.IssuingAuthority = CertificateServiceSupport.Optional(request.IssuingAuthority);
        item.IssuedOn = request.IssuedOn;
        item.ExpiresOn = request.ExpiresOn;
        item.Notes = CertificateServiceSupport.Optional(request.Notes);
        item.ConcurrencyStamp = Guid.NewGuid();
        item.UpdatedAt = DateTimeOffset.UtcNow;
        AddAudit(userId, request.Id.HasValue ? "Update" : "Create", item, reason, before, JsonSerializer.Serialize(Snapshot(item)));
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(item.Id, today, cancellationToken);
    }

    public async Task DeleteAsync(string userId, bool canManage, Guid id, Guid concurrencyStamp, string reason, CancellationToken cancellationToken)
    {
        EnsureManage(canManage);
        var item = await db.EmployeeCertificates.Include(value => value.Attachment).SingleOrDefaultAsync(value => value.Id == id && !value.IsDeleted, cancellationToken) ?? throw new KeyNotFoundException("员工证书不存在。");
        if (item.ConcurrencyStamp != concurrencyStamp) throw new DbUpdateConcurrencyException("员工证书已被其他用户修改。");
        var before = JsonSerializer.Serialize(Snapshot(item));
        item.IsDeleted = true;
        item.ConcurrencyStamp = Guid.NewGuid();
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await CertificateServiceSupport.RemoveAttachmentAsync(item.Attachment, fileStore, cancellationToken);
        AddAudit(userId, "Delete", item, CertificateServiceSupport.Required(reason, nameof(reason)), before, JsonSerializer.Serialize(Snapshot(item)));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CertificateFileDto> DownloadAttachmentAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await Query().SingleOrDefaultAsync(value => value.Id == id && !value.IsDeleted, cancellationToken) ?? throw new KeyNotFoundException("员工证书不存在。");
        return await CertificateServiceSupport.DownloadAsync(item.Attachment, fileStore, cancellationToken);
    }

    private IQueryable<EmployeeCertificate> Query() => db.EmployeeCertificates.AsNoTracking().Include(item => item.Employee).Include(item => item.Attachment);
    private static void EnsureManage(bool canManage) { if (!canManage) throw new UnauthorizedAccessException("当前用户没有员工证书维护权限。"); }
    private void AddAudit(string userId, string action, EmployeeCertificate item, string reason, string? before, string? after) => db.AuditLogs.Add(new AuditLog { UserId = userId, Action = action, EntityType = nameof(EmployeeCertificate), EntityId = item.Id.ToString(), Reason = reason, BeforeJson = before, AfterJson = after });
    private static object Snapshot(EmployeeCertificate item) => new { item.EmployeeId, item.CertificateType, item.CertificateNumber, item.SpecialtyLevelScope, item.IssuingAuthority, item.IssuedOn, item.ExpiresOn, item.AttachmentId, item.Notes, item.IsDeleted, item.ConcurrencyStamp };
    private static EmployeeCertificateDto ToDto(EmployeeCertificate item, DateOnly today) => new(item.Id, item.EmployeeId, item.Employee.EmployeeNumber, item.Employee.Name, item.CertificateType, item.CertificateNumber, item.SpecialtyLevelScope, item.IssuingAuthority, item.IssuedOn, item.ExpiresOn, item.AttachmentId, item.Attachment?.OriginalFileName, item.Notes, CertificateExpiryCalculator.GetState(today, item.ExpiresOn), item.ConcurrencyStamp);
}
