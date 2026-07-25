using System.Text.Json;
using EngineeringManager.Application.Certificates;
using EngineeringManager.Application.Companies;
using EngineeringManager.Domain.Certificates;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Files;
using EngineeringManager.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Certificates;

public sealed class CompanyCertificateService(ApplicationDbContext db, IFileStore fileStore) : ICompanyCertificateService
{
    public async Task<IReadOnlyList<CompanyCertificateItemDto>> ListAsync(CompanyActor actor, CertificateFilter filter, DateOnly today, CancellationToken cancellationToken)
    {
        var query = Authorized(actor).AsNoTracking().Include(item => item.LegalEntity).Include(item => item.Attachment).Where(item => !item.IsDeleted);
        if (filter.OwnerId.HasValue) query = query.Where(item => item.LegalEntityId == filter.OwnerId);
        if (!string.IsNullOrWhiteSpace(filter.CertificateType)) query = query.Where(item => item.CertificateType == filter.CertificateType.Trim());
        foreach (var term in SearchTerms.Parse(filter.Search))
        {
            var hasDate = SearchTerms.TryParseDate(term, out var date);
            query = query.Where(item =>
                item.LegalEntity.Code.Contains(term)
                || item.LegalEntity.Name.Contains(term)
                || item.LegalEntity.ShortName.Contains(term)
                || item.CertificateType.Contains(term)
                || (item.CertificateNumber != null && item.CertificateNumber.Contains(term))
                || (item.SpecialtyLevelScope != null && item.SpecialtyLevelScope.Contains(term))
                || (item.IssuingAuthority != null && item.IssuingAuthority.Contains(term))
                || (item.Notes != null && item.Notes.Contains(term))
                || (item.Attachment != null && item.Attachment.OriginalFileName.Contains(term))
                || (hasDate && (item.IssuedOn == date || item.ExpiresOn == date)));
        }
        var items = await query.OrderBy(item => item.ExpiresOn == null).ThenBy(item => item.ExpiresOn).ThenBy(item => item.LegalEntity.Code).ToListAsync(cancellationToken);
        var result = items.Select(item => ToDto(item, today)).ToArray();
        return filter.State.HasValue ? result.Where(item => item.State == filter.State).ToArray() : result;
    }

    public async Task<CompanyCertificateItemDto> GetAsync(CompanyActor actor, Guid id, DateOnly today, CancellationToken cancellationToken)
    {
        var item = await Authorized(actor).AsNoTracking().Include(value => value.LegalEntity).Include(value => value.Attachment)
            .SingleOrDefaultAsync(value => value.Id == id && !value.IsDeleted, cancellationToken) ?? throw new KeyNotFoundException("公司证书不存在或无权访问。");
        return ToDto(item, today);
    }

    public async Task<CompanyCertificateItemDto> SaveAsync(CompanyActor actor, SaveCompanyCertificateItemRequest request, DateOnly today, CancellationToken cancellationToken)
    {
        EnsureManage(actor);
        var reason = CertificateServiceSupport.Required(request.Reason, nameof(request.Reason));
        CertificateServiceSupport.ValidateDates(request.IssuedOn, request.ExpiresOn, nameof(request));
        if (!await AuthorizedCompanies(actor).AnyAsync(item => item.Id == request.LegalEntityId, cancellationToken)) throw new KeyNotFoundException("自有公司不存在或无权访问。");
        CompanyCertificate item;
        string? before = null;
        if (request.Id.HasValue)
        {
            item = await Authorized(actor).Include(value => value.Attachment).SingleOrDefaultAsync(value => value.Id == request.Id && !value.IsDeleted, cancellationToken) ?? throw new KeyNotFoundException("公司证书不存在或无权访问。");
            if (!request.ConcurrencyStamp.HasValue || request.ConcurrencyStamp != item.ConcurrencyStamp) throw new DbUpdateConcurrencyException("公司证书已被其他用户修改。");
            before = JsonSerializer.Serialize(Snapshot(item));
        }
        else
        {
            item = new CompanyCertificate();
            db.CompanyCertificates.Add(item);
        }
        if (request.NewAttachment is not null)
        {
            await CertificateServiceSupport.RemoveAttachmentAsync(item.Attachment, fileStore, cancellationToken);
            item.Attachment = await CertificateServiceSupport.SaveAttachmentAsync(db, fileStore, request.NewAttachment, actor.UserId, cancellationToken);
        }
        else if (request.RemoveAttachment)
        {
            await CertificateServiceSupport.RemoveAttachmentAsync(item.Attachment, fileStore, cancellationToken);
            item.AttachmentId = null;
            item.Attachment = null;
        }
        item.LegalEntityId = request.LegalEntityId;
        item.CertificateType = CertificateServiceSupport.Required(request.CertificateType, nameof(request.CertificateType));
        item.CertificateNumber = CertificateServiceSupport.Optional(request.CertificateNumber);
        item.SpecialtyLevelScope = CertificateServiceSupport.Optional(request.SpecialtyLevelScope);
        item.IssuingAuthority = CertificateServiceSupport.Optional(request.IssuingAuthority);
        item.IssuedOn = request.IssuedOn;
        item.ExpiresOn = request.ExpiresOn;
        item.Notes = CertificateServiceSupport.Optional(request.Notes);
        item.ConcurrencyStamp = Guid.NewGuid();
        item.UpdatedAt = DateTimeOffset.UtcNow;
        AddAudit(actor.UserId, request.Id.HasValue ? "Update" : "Create", item, reason, before, JsonSerializer.Serialize(Snapshot(item)));
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(actor, item.Id, today, cancellationToken);
    }

    public async Task<IReadOnlyList<CompanyCertificateItemDto>> SaveManyAsync(
        CompanyActor actor,
        IReadOnlyList<SaveCompanyCertificateItemRequest> requests,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        EnsureManage(actor);
        if (requests.Count == 0) throw new ArgumentException("没有可保存的公司证照。", nameof(requests));
        if (requests.Any(request => !request.Id.HasValue)) throw new ArgumentException("批量修改只能保存已有公司证照。", nameof(requests));
        if (requests.Any(request => request.NewAttachment is not null || request.RemoveAttachment))
            throw new ArgumentException("批量修改不处理证书附件。", nameof(requests));

        var ids = requests.Select(request => request.Id!.Value).ToArray();
        if (ids.Distinct().Count() != ids.Length) throw new ArgumentException("批量修改中存在重复的公司证照。", nameof(requests));
        var prepared = requests.Select(request => new
        {
            Request = request,
            CertificateType = CertificateServiceSupport.Required(request.CertificateType, nameof(request.CertificateType)),
            CertificateNumber = CertificateServiceSupport.Optional(request.CertificateNumber),
            Notes = CertificateServiceSupport.Optional(request.Notes),
            Reason = CertificateServiceSupport.Required(request.Reason, nameof(request.Reason))
        }).ToList();
        foreach (var item in prepared) CertificateServiceSupport.ValidateDates(item.Request.IssuedOn, item.Request.ExpiresOn, nameof(requests));

        var certificates = await Authorized(actor).Where(item => ids.Contains(item.Id) && !item.IsDeleted).ToListAsync(cancellationToken);
        if (certificates.Count != ids.Length) throw new KeyNotFoundException("公司证书不存在或无权访问。");
        var certificateById = certificates.ToDictionary(item => item.Id);
        foreach (var item in prepared)
        {
            var certificate = certificateById[item.Request.Id!.Value];
            if (certificate.LegalEntityId != item.Request.LegalEntityId) throw new KeyNotFoundException("公司证书不存在或无权访问。");
            if (!item.Request.ConcurrencyStamp.HasValue || item.Request.ConcurrencyStamp != certificate.ConcurrencyStamp)
                throw new DbUpdateConcurrencyException("公司证书已被其他用户修改。");
        }

        foreach (var item in prepared)
        {
            var certificate = certificateById[item.Request.Id!.Value];
            var before = JsonSerializer.Serialize(Snapshot(certificate));
            certificate.CertificateType = item.CertificateType;
            certificate.CertificateNumber = item.CertificateNumber;
            certificate.IssuedOn = item.Request.IssuedOn;
            certificate.ExpiresOn = item.Request.ExpiresOn;
            certificate.Notes = item.Notes;
            certificate.ConcurrencyStamp = Guid.NewGuid();
            certificate.UpdatedAt = DateTimeOffset.UtcNow;
            AddAudit(actor.UserId, "Update", certificate, item.Reason, before, JsonSerializer.Serialize(Snapshot(certificate)));
        }

        await db.SaveChangesAsync(cancellationToken);
        var saved = await Authorized(actor).AsNoTracking().Include(item => item.LegalEntity).Include(item => item.Attachment)
            .Where(item => ids.Contains(item.Id) && !item.IsDeleted).ToListAsync(cancellationToken);
        var savedById = saved.ToDictionary(item => item.Id);
        return ids.Select(id => ToDto(savedById[id], today)).ToList();
    }

    public async Task DeleteAsync(CompanyActor actor, Guid id, Guid concurrencyStamp, string reason, CancellationToken cancellationToken)
    {
        EnsureManage(actor);
        var item = await Authorized(actor).Include(value => value.Attachment).SingleOrDefaultAsync(value => value.Id == id && !value.IsDeleted, cancellationToken) ?? throw new KeyNotFoundException("公司证书不存在或无权访问。");
        if (item.ConcurrencyStamp != concurrencyStamp) throw new DbUpdateConcurrencyException("公司证书已被其他用户修改。");
        var before = JsonSerializer.Serialize(Snapshot(item));
        item.IsDeleted = true;
        item.ConcurrencyStamp = Guid.NewGuid();
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await CertificateServiceSupport.RemoveAttachmentAsync(item.Attachment, fileStore, cancellationToken);
        AddAudit(actor.UserId, "Delete", item, CertificateServiceSupport.Required(reason, nameof(reason)), before, JsonSerializer.Serialize(Snapshot(item)));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CertificateFileDto> DownloadAttachmentAsync(CompanyActor actor, Guid id, CancellationToken cancellationToken)
    {
        var item = await Authorized(actor).AsNoTracking().Include(value => value.Attachment).SingleOrDefaultAsync(value => value.Id == id && !value.IsDeleted, cancellationToken) ?? throw new KeyNotFoundException("公司证书不存在或无权访问。");
        return await CertificateServiceSupport.DownloadAsync(item.Attachment, fileStore, cancellationToken);
    }

    private IQueryable<CompanyCertificate> Authorized(CompanyActor actor)
    {
        var query = db.CompanyCertificates.AsQueryable();
        if (!actor.CanAccessAllCompanies)
        {
            var ids = actor.AccessibleCompanyIds.ToHashSet();
            query = query.Where(item => ids.Contains(item.LegalEntityId));
        }
        return query;
    }
    private IQueryable<EngineeringManager.Domain.Organization.LegalEntity> AuthorizedCompanies(CompanyActor actor)
    {
        var query = db.LegalEntities.AsQueryable();
        if (!actor.CanAccessAllCompanies)
        {
            var ids = actor.AccessibleCompanyIds.ToHashSet();
            query = query.Where(item => ids.Contains(item.Id));
        }
        return query;
    }
    private static void EnsureManage(CompanyActor actor) { if (!actor.CanManage) throw new UnauthorizedAccessException("当前用户没有公司证书维护权限。"); }
    private void AddAudit(string userId, string action, CompanyCertificate item, string reason, string? before, string? after) => db.AuditLogs.Add(new AuditLog { UserId = userId, Action = action, EntityType = nameof(CompanyCertificate), EntityId = item.Id.ToString(), Reason = reason, BeforeJson = before, AfterJson = after });
    private static object Snapshot(CompanyCertificate item) => new { item.LegalEntityId, item.CertificateType, item.CertificateNumber, item.SpecialtyLevelScope, item.IssuingAuthority, item.IssuedOn, item.ExpiresOn, item.AttachmentId, item.Notes, item.IsDeleted, item.ConcurrencyStamp };
    private static CompanyCertificateItemDto ToDto(CompanyCertificate item, DateOnly today) => new(item.Id, item.LegalEntityId, item.LegalEntity.Code, item.LegalEntity.Name, item.CertificateType, item.CertificateNumber, item.SpecialtyLevelScope, item.IssuingAuthority, item.IssuedOn, item.ExpiresOn, item.AttachmentId, item.Attachment?.OriginalFileName, item.Notes, CertificateExpiryCalculator.GetState(today, item.ExpiresOn), item.ConcurrencyStamp);
}
