using System.Text.Json;
using EngineeringManager.Application.Companies;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Companies;

public sealed class CompanyManagementService(ApplicationDbContext db) : ICompanyManagementService
{
    public async Task<IReadOnlyList<CompanyListItemDto>> ListAsync(CompanyActor actor, CancellationToken cancellationToken)
    {
        var query = AuthorizedCompanies(actor).AsNoTracking();
        return await query.OrderBy(item => item.Code).Select(item => new CompanyListItemDto(
            item.Id, item.Code, item.Name, item.ShortName,
            item.CompanyCategory == null ? null : item.CompanyCategory.Name,
            item.LegalRepresentative, item.IsActive, item.Notes,
            db.FinancialAccounts.Count(account => account.LegalEntityId == item.Id && account.IsActive),
            db.FinancialAccounts.Count(account => account.LegalEntityId == item.Id))).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CompanyListItemDto>> SearchAsync(CompanyActor actor, string? search, CancellationToken cancellationToken)
    {
        IQueryable<LegalEntity> query = AuthorizedCompanies(actor).AsNoTracking().Include(item => item.CompanyCategory);
        foreach (var term in SearchTerms.Parse(search))
        {
            var hasDate = SearchTerms.TryParseDate(term, out var date);
            var hasAmount = SearchTerms.TryParseDecimal(term, out var amount);
            query = query.Where(item =>
                item.Code.Contains(term)
                || item.Name.Contains(term)
                || item.ShortName.Contains(term)
                || (item.CompanyCategory != null && (item.CompanyCategory.Code.Contains(term) || item.CompanyCategory.Name.Contains(term)))
                || (item.LegalRepresentative != null && item.LegalRepresentative.Contains(term))
                || (item.UnifiedSocialCreditCode != null && item.UnifiedSocialCreditCode.Contains(term))
                || (item.RegisteredAddress != null && item.RegisteredAddress.Contains(term))
                || (item.BusinessAddress != null && item.BusinessAddress.Contains(term))
                || (item.Phone != null && item.Phone.Contains(term))
                || (item.InvoiceTitle != null && item.InvoiceTitle.Contains(term))
                || (item.Notes != null && item.Notes.Contains(term))
                || db.FinancialAccounts.Any(account => account.LegalEntityId == item.Id && (
                    account.AccountName.Contains(term)
                    || (account.BankName != null && account.BankName.Contains(term))
                    || (account.Notes != null && account.Notes.Contains(term))
                    || (actor.CanManage && account.AccountNumber != null && account.AccountNumber.Contains(term))
                    || (hasAmount && account.OpeningBalance == amount)))
                || db.CompanyCertificates.Any(certificate => certificate.LegalEntityId == item.Id && !certificate.IsDeleted && (
                    certificate.CertificateType.Contains(term)
                    || (certificate.CertificateNumber != null && certificate.CertificateNumber.Contains(term))
                    || (certificate.SpecialtyLevelScope != null && certificate.SpecialtyLevelScope.Contains(term))
                    || (certificate.IssuingAuthority != null && certificate.IssuingAuthority.Contains(term))
                    || (certificate.Notes != null && certificate.Notes.Contains(term))
                    || (hasDate && (certificate.IssuedOn == date || certificate.ExpiresOn == date)))));
        }

        return await query.OrderBy(item => item.Code).Select(item => new CompanyListItemDto(
            item.Id, item.Code, item.Name, item.ShortName,
            item.CompanyCategory == null ? null : item.CompanyCategory.Name,
            item.LegalRepresentative, item.IsActive, item.Notes,
            db.FinancialAccounts.Count(account => account.LegalEntityId == item.Id && account.IsActive),
            db.FinancialAccounts.Count(account => account.LegalEntityId == item.Id))).ToListAsync(cancellationToken);
    }

    public async Task<CompanyDetailsDto> GetAsync(CompanyActor actor, Guid id, CancellationToken cancellationToken)
    {
        var entity = await AuthorizedCompanies(actor).AsNoTracking()
            .Include(item => item.CompanyCategory)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("自有公司不存在或无权访问。");
        return await ToDetailsAsync(entity, cancellationToken);
    }

    public async Task<CompanyDetailsDto> SaveCompanyAsync(CompanyActor actor, SaveCompanyRequest request, CancellationToken cancellationToken)
    {
        EnsureManage(actor);
        var code = Required(request.Code, nameof(request.Code));
        var name = Required(request.Name, nameof(request.Name));
        var shortName = Required(request.ShortName, nameof(request.ShortName));
        var reason = Required(request.Reason, nameof(request.Reason));
        if (!request.CompanyCategoryId.HasValue)
        {
            throw new ArgumentException("请选择公司组合分类。", nameof(request));
        }
        if (!await db.CompanyCategories.AnyAsync(
                item => item.Id == request.CompanyCategoryId && item.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("公司组合分类不存在或已停用。");
        }

        var duplicateCode = await db.LegalEntities.AnyAsync(
            item => item.Code == code && (!request.Id.HasValue || item.Id != request.Id), cancellationToken);
        if (duplicateCode)
        {
            throw new InvalidOperationException($"公司编码已存在：{code}");
        }

        var taxCode = Optional(request.UnifiedSocialCreditCode);
        if (taxCode is not null && await db.LegalEntities.AnyAsync(
                item => item.UnifiedSocialCreditCode == taxCode && (!request.Id.HasValue || item.Id != request.Id), cancellationToken))
        {
            throw new InvalidOperationException("统一社会信用代码/税号已存在。");
        }

        LegalEntity entity;
        string? before = null;
        if (request.Id.HasValue)
        {
            entity = await AuthorizedCompanies(actor).SingleOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
                ?? throw new KeyNotFoundException("自有公司不存在或无权访问。");
            if (!request.ConcurrencyStamp.HasValue || request.ConcurrencyStamp.Value != entity.ConcurrencyStamp)
            {
                throw new DbUpdateConcurrencyException("公司资料已被其他用户修改，请刷新后重试。");
            }
            before = JsonSerializer.Serialize(Snapshot(entity));
        }
        else
        {
            entity = new LegalEntity();
            db.LegalEntities.Add(entity);
        }

        entity.Code = code;
        entity.Name = name;
        entity.ShortName = shortName;
        entity.CompanyCategoryId = request.CompanyCategoryId;
        entity.LegalRepresentative = Optional(request.LegalRepresentative);
        entity.UnifiedSocialCreditCode = taxCode;
        entity.RegisteredAddress = Optional(request.RegisteredAddress);
        entity.BusinessAddress = Optional(request.BusinessAddress);
        entity.Phone = Optional(request.Phone);
        entity.InvoiceTitle = Optional(request.InvoiceTitle) ?? name;
        entity.Notes = Optional(request.Notes);
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.ConcurrencyStamp = Guid.NewGuid();
        db.AuditLogs.Add(new AuditLog
        {
            UserId = actor.UserId,
            Action = request.Id.HasValue ? "Update" : "Create",
            EntityType = nameof(LegalEntity),
            EntityId = entity.Id.ToString(),
            Reason = reason,
            BeforeJson = before,
            AfterJson = JsonSerializer.Serialize(Snapshot(entity))
        });
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(actor, entity.Id, cancellationToken);
    }

    public async Task<SaveCompanyRequest> PrepareCopyAsync(CompanyActor actor, Guid sourceId, CancellationToken cancellationToken)
    {
        var source = await GetAsync(actor, sourceId, cancellationToken);
        return new SaveCompanyRequest(null, string.Empty, $"{source.Name} - 副本", $"{source.ShortName}副本",
            source.CompanyCategoryId, source.LegalRepresentative, null, source.RegisteredAddress,
            source.BusinessAddress, source.Phone, $"{source.Name} - 副本", source.Notes, null, "复制公司档案");
    }

    public async Task<IReadOnlyList<CompanyCategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken) =>
        await db.CompanyCategories.AsNoTracking().OrderBy(item => item.SortOrder).ThenBy(item => item.Name)
            .Select(item => new CompanyCategoryDto(item.Id, item.Code, item.Name, item.SortOrder, item.IsActive, item.ConcurrencyStamp))
            .ToListAsync(cancellationToken);

    public async Task<CompanyCategoryDto> SaveCategoryAsync(CompanyActor actor, SaveCompanyCategoryRequest request, CancellationToken cancellationToken)
    {
        EnsureManage(actor);
        var code = Required(request.Code, nameof(request.Code));
        var name = Required(request.Name, nameof(request.Name));
        CompanyCategory category;
        if (request.Id.HasValue)
        {
            category = await db.CompanyCategories.SingleOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
                ?? throw new KeyNotFoundException("公司组合分类不存在。");
            if (!request.ConcurrencyStamp.HasValue || request.ConcurrencyStamp.Value != category.ConcurrencyStamp)
            {
                throw new DbUpdateConcurrencyException("公司组合分类已被其他用户修改。");
            }
        }
        else
        {
            category = new CompanyCategory();
            db.CompanyCategories.Add(category);
        }
        if (await db.CompanyCategories.AnyAsync(item => item.Code == code && item.Id != category.Id, cancellationToken))
        {
            throw new InvalidOperationException($"公司组合分类编码已存在：{code}");
        }
        category.Code = code;
        category.Name = name;
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;
        category.ConcurrencyStamp = Guid.NewGuid();
        category.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new CompanyCategoryDto(category.Id, category.Code, category.Name, category.SortOrder, category.IsActive, category.ConcurrencyStamp);
    }

    public async Task<CompanyAccountDto> SaveAccountAsync(CompanyActor actor, SaveCompanyAccountRequest request, CancellationToken cancellationToken)
    {
        EnsureManage(actor);
        await EnsureAccessAsync(actor, request.LegalEntityId, cancellationToken);
        var reason = Required(request.Reason, nameof(request.Reason));
        FinancialAccount account;
        string? before = null;
        if (request.Id.HasValue)
        {
            account = await db.FinancialAccounts.SingleOrDefaultAsync(item => item.Id == request.Id && item.LegalEntityId == request.LegalEntityId, cancellationToken)
                ?? throw new KeyNotFoundException("公司账户不存在。");
            if (!request.ConcurrencyStamp.HasValue || request.ConcurrencyStamp.Value != account.ConcurrencyStamp)
            {
                throw new DbUpdateConcurrencyException("公司账户已被其他用户修改。");
            }
            before = JsonSerializer.Serialize(Snapshot(account));
        }
        else
        {
            account = new FinancialAccount { LegalEntityId = request.LegalEntityId };
            db.FinancialAccounts.Add(account);
        }
        account.AccountName = Required(request.AccountName, nameof(request.AccountName));
        account.AccountNumber = Optional(request.AccountNumber);
        account.BankName = Optional(request.BankName);
        account.AccountType = Enum.IsDefined((FinancialAccountType)request.AccountType) ? (FinancialAccountType)request.AccountType : throw new ArgumentOutOfRangeException(nameof(request));
        account.OpeningBalance = request.OpeningBalance;
        account.Notes = Optional(request.Notes);
        var isActive = request.IsActive;
        account.IsActive = isActive;
        account.IsDefaultCollection = isActive && request.IsDefaultCollection;
        account.IsDefaultPayment = isActive && request.IsDefaultPayment;
        account.IsDefaultInvoice = isActive && request.IsDefaultInvoice;
        account.ConcurrencyStamp = Guid.NewGuid();
        var siblings = await db.FinancialAccounts.AsNoTracking().Where(item => item.LegalEntityId == request.LegalEntityId && item.Id != account.Id)
            .Select(item => new CompanyAccountDefault(item.IsDefaultCollection, item.IsDefaultPayment, item.IsDefaultInvoice)).ToListAsync(cancellationToken);
        siblings.Add(new CompanyAccountDefault(account.IsDefaultCollection, account.IsDefaultPayment, account.IsDefaultInvoice));
        CompanyAccountRules.Validate(siblings);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = actor.UserId,
            Action = request.Id.HasValue ? "Update" : "Create",
            EntityType = nameof(FinancialAccount),
            EntityId = account.Id.ToString(),
            Reason = reason,
            BeforeJson = before,
            AfterJson = JsonSerializer.Serialize(Snapshot(account))
        });
        await db.SaveChangesAsync(cancellationToken);
        return ToAccount(account);
    }

    public async Task<CompanyCertificateDto> SaveCertificateAsync(CompanyActor actor, SaveCompanyCertificateRequest request, CancellationToken cancellationToken)
    {
        EnsureManage(actor);
        await EnsureAccessAsync(actor, request.LegalEntityId, cancellationToken);
        if (request.ExpiresOn.HasValue && request.IssuedOn.HasValue && request.ExpiresOn < request.IssuedOn)
        {
            throw new ArgumentException("证照有效期不能早于签发日期。", nameof(request));
        }
        CompanyCertificate certificate;
        if (request.Id.HasValue)
        {
            certificate = await db.CompanyCertificates.SingleOrDefaultAsync(item => item.Id == request.Id && item.LegalEntityId == request.LegalEntityId, cancellationToken)
                ?? throw new KeyNotFoundException("公司证照不存在。");
            if (!request.ConcurrencyStamp.HasValue || request.ConcurrencyStamp.Value != certificate.ConcurrencyStamp)
            {
                throw new DbUpdateConcurrencyException("公司证照已被其他用户修改。");
            }
        }
        else
        {
            certificate = new CompanyCertificate { LegalEntityId = request.LegalEntityId };
            db.CompanyCertificates.Add(certificate);
        }
        certificate.CertificateType = Required(request.CertificateType, nameof(request.CertificateType));
        certificate.CertificateNumber = Optional(request.CertificateNumber);
        certificate.IssuedOn = request.IssuedOn;
        certificate.ExpiresOn = request.ExpiresOn;
        certificate.AttachmentId = request.AttachmentId;
        certificate.Notes = Optional(request.Notes);
        certificate.ConcurrencyStamp = Guid.NewGuid();
        certificate.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToCertificate(certificate);
    }

    public async Task<CompanyWorkspaceSummaryDto> GetWorkspaceSummaryAsync(CompanyActor actor, Guid companyId, CancellationToken cancellationToken)
    {
        await EnsureAccessAsync(actor, companyId, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var projectIds = await GetCompanyProjectIdsAsync(companyId, cancellationToken);
        var contractCount = await db.ContractLegalEntityAllocations.AsNoTracking()
            .CountAsync(item => item.LegalEntityId == companyId, cancellationToken);
        var accounts = await db.FinancialAccounts.AsNoTracking()
            .Where(item => item.LegalEntityId == companyId)
            .Select(item => item.IsActive)
            .ToListAsync(cancellationToken);
        var certificates = await db.CompanyCertificates.AsNoTracking()
            .Where(item => item.LegalEntityId == companyId && !item.IsDeleted)
            .Select(item => item.ExpiresOn)
            .ToListAsync(cancellationToken);
        var expired = certificates.Count(item => item.HasValue && item.Value < today);
        return new CompanyWorkspaceSummaryDto(
            projectIds.Count,
            contractCount,
            accounts.Count(item => item),
            accounts.Count,
            certificates.Count - expired,
            certificates.Count,
            expired);
    }

    public async Task<IReadOnlyList<CompanyActivityItemDto>> ListRecentActivityAsync(CompanyActor actor, Guid companyId, int take, CancellationToken cancellationToken)
    {
        await EnsureAccessAsync(actor, companyId, cancellationToken);
        take = NormalizeTake(take, 10);
        var projectIds = await GetCompanyProjectIdsAsync(companyId, cancellationToken);
        var activities = new List<CompanyActivityItemDto>();

        if (projectIds.Count > 0)
        {
            var projectActivityRows = await db.Projects.AsNoTracking()
                .Where(item => projectIds.Contains(item.Id))
                .Select(item => new { item.Id, item.ProjectNumber, item.Name, item.CreatedAt, item.UpdatedAt })
                .ToListAsync(cancellationToken);
            var projects = projectActivityRows
                .OrderByDescending(item => item.UpdatedAt)
                .Take(take)
                .ToList();
            activities.AddRange(projects.Select(item => new CompanyActivityItemDto(
                "project",
                item.ProjectNumber + " " + item.Name,
                "关联项目",
                null,
                DateOnly.FromDateTime(item.CreatedAt.UtcDateTime),
                item.Id,
                item.Id)));
        }

        var contractActivityRows = await db.ContractLegalEntityAllocations.AsNoTracking()
            .Where(item => item.LegalEntityId == companyId)
            .Select(item => new
            {
                item.ContractId,
                item.Contract.ProjectId,
                item.Contract.ContractNumber,
                item.Contract.Name,
                item.Contract.TotalAmount,
                item.Contract.SignedDate,
                item.Contract.CreatedAt,
                item.Contract.UpdatedAt
            })
            .ToListAsync(cancellationToken);
        var contracts = contractActivityRows
            .OrderByDescending(item => item.UpdatedAt)
            .Take(take)
            .ToList();
        activities.AddRange(contracts.Select(item => new CompanyActivityItemDto(
            "contract",
            item.ContractNumber + " " + item.Name,
            "合同",
            item.TotalAmount,
            item.SignedDate ?? DateOnly.FromDateTime(item.CreatedAt.UtcDateTime),
            item.ProjectId,
            item.ContractId)));

        var collections = await db.CollectionEntries.AsNoTracking()
            .Where(item => item.LegalEntityId == companyId)
            .OrderByDescending(item => item.CollectionDate)
            .Take(take)
            .Select(item => new
            {
                item.Id,
                item.ProjectId,
                item.Project.ProjectNumber,
                item.Project.Name,
                item.Amount,
                item.CollectionDate,
                item.Notes
            })
            .ToListAsync(cancellationToken);
        activities.AddRange(collections.Select(item => new CompanyActivityItemDto(
            "collection",
            item.ProjectNumber + " 收款",
            string.IsNullOrWhiteSpace(item.Notes) ? item.Name : item.Notes,
            item.Amount,
            item.CollectionDate,
            item.ProjectId,
            item.Id)));

        var payments = await db.PaymentEntries.AsNoTracking()
            .Where(item => item.LegalEntityId == companyId)
            .OrderByDescending(item => item.PaymentDate)
            .Take(take)
            .Select(item => new
            {
                item.Id,
                item.ProjectId,
                item.Project.ProjectNumber,
                item.Project.Name,
                item.Amount,
                item.PaymentDate,
                item.Notes
            })
            .ToListAsync(cancellationToken);
        activities.AddRange(payments.Select(item => new CompanyActivityItemDto(
            "payment",
            item.ProjectNumber + " 付款",
            string.IsNullOrWhiteSpace(item.Notes) ? item.Name : item.Notes,
            item.Amount,
            item.PaymentDate,
            item.ProjectId,
            item.Id)));

        var invoices = await db.InvoiceEntries.AsNoTracking()
            .Where(item => item.LegalEntityId == companyId)
            .OrderByDescending(item => item.InvoiceDate)
            .Take(take)
            .Select(item => new
            {
                item.Id,
                item.ProjectId,
                item.Project.ProjectNumber,
                item.InvoiceNumber,
                item.GrossAmount,
                item.InvoiceDate,
                item.Direction
            })
            .ToListAsync(cancellationToken);
        activities.AddRange(invoices.Select(item => new CompanyActivityItemDto(
            "invoice",
            item.InvoiceNumber,
            item.ProjectNumber + " " + (item.Direction == InvoiceDirection.Output ? "销项发票" : "进项发票"),
            item.GrossAmount,
            item.InvoiceDate,
            item.ProjectId,
            item.Id)));

        return activities
            .OrderByDescending(item => item.Date ?? DateOnly.MinValue)
            .ThenByDescending(item => item.Title)
            .Take(take)
            .ToArray();
    }

    public async Task<IReadOnlyList<CompanyProjectRowDto>> ListCompanyProjectsAsync(CompanyActor actor, Guid companyId, string? search, int take, CancellationToken cancellationToken)
    {
        await EnsureAccessAsync(actor, companyId, cancellationToken);
        take = NormalizeTake(take, 50);
        var projectIds = await GetCompanyProjectIdsAsync(companyId, cancellationToken);
        if (projectIds.Count == 0)
        {
            return [];
        }

        var query = db.Projects.AsNoTracking().Where(item => projectIds.Contains(item.Id));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.ProjectNumber.Contains(term) || item.Name.Contains(term));
        }

        var projectRows = await query
            .Select(item => new { item.Id, item.ProjectNumber, item.Name, item.Stage, item.UpdatedAt })
            .ToListAsync(cancellationToken);
        var projects = projectRows
            .OrderByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.ProjectNumber)
            .Take(take)
            .Select(item => new { item.Id, item.ProjectNumber, item.Name, item.Stage })
            .ToList();
        var selectedIds = projects.Select(item => item.Id).ToHashSet();

        var shareRows = await db.ContractLegalEntityAllocations.AsNoTracking()
            .Where(item => item.LegalEntityId == companyId && selectedIds.Contains(item.Contract.ProjectId))
            .Select(item => new { item.Contract.ProjectId, item.Amount, item.Percentage, item.Contract.TotalAmount })
            .ToListAsync(cancellationToken);
        var shareByProject = shareRows
            .GroupBy(item => item.ProjectId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount ?? item.TotalAmount * (item.Percentage ?? 0m) / 100m));

        var receivables = await db.ReceivableEntries.AsNoTracking()
            .Where(item => item.LegalEntityId == companyId && !item.IsVoided && selectedIds.Contains(item.ProjectId))
            .GroupBy(item => item.ProjectId)
            .Select(group => new { ProjectId = group.Key, Amount = group.Sum(item => item.Amount) })
            .ToListAsync(cancellationToken);
        var collections = await db.CollectionEntries.AsNoTracking()
            .Where(item => item.LegalEntityId == companyId && selectedIds.Contains(item.ProjectId))
            .GroupBy(item => item.ProjectId)
            .Select(group => new { ProjectId = group.Key, Amount = group.Sum(item => item.Amount) })
            .ToListAsync(cancellationToken);
        var refunds = await db.RefundOrReversalEntries.AsNoTracking()
            .Where(item =>
                ((item.Receivable != null && item.Receivable.LegalEntityId == companyId) ||
                 (item.Collection != null && item.Collection.LegalEntityId == companyId)) &&
                ((item.Receivable != null && selectedIds.Contains(item.Receivable.ProjectId)) ||
                 (item.Collection != null && selectedIds.Contains(item.Collection.ProjectId))))
            .Select(item => new
            {
                ProjectId = item.Receivable != null ? item.Receivable.ProjectId : item.Collection!.ProjectId,
                item.Amount
            })
            .ToListAsync(cancellationToken);
        var payables = await db.PayableEntries.AsNoTracking()
            .Where(item => item.LegalEntityId == companyId && !item.IsVoided && selectedIds.Contains(item.ProjectId))
            .GroupBy(item => item.ProjectId)
            .Select(group => new { ProjectId = group.Key, Amount = group.Sum(item => item.Amount) })
            .ToListAsync(cancellationToken);
        var payments = await db.PaymentEntries.AsNoTracking()
            .Where(item => item.LegalEntityId == companyId && selectedIds.Contains(item.ProjectId))
            .GroupBy(item => item.ProjectId)
            .Select(group => new { ProjectId = group.Key, Amount = group.Sum(item => item.Amount) })
            .ToListAsync(cancellationToken);
        var reversals = await db.PaymentReversalEntries.AsNoTracking()
            .Where(item => item.Payment.LegalEntityId == companyId && selectedIds.Contains(item.Payment.ProjectId))
            .GroupBy(item => item.Payment.ProjectId)
            .Select(group => new { ProjectId = group.Key, Amount = group.Sum(item => item.Amount) })
            .ToListAsync(cancellationToken);

        var receivableMap = receivables.ToDictionary(item => item.ProjectId, item => item.Amount);
        var collectedMap = collections.ToDictionary(item => item.ProjectId, item => item.Amount);
        var refundMap = refunds.GroupBy(item => item.ProjectId).ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));
        var payableMap = payables.ToDictionary(item => item.ProjectId, item => item.Amount);
        var paidMap = payments.ToDictionary(item => item.ProjectId, item => item.Amount);
        var reversalMap = reversals.ToDictionary(item => item.ProjectId, item => item.Amount);

        return projects.Select(item =>
        {
            var collected = collectedMap.GetValueOrDefault(item.Id) - refundMap.GetValueOrDefault(item.Id);
            var paid = paidMap.GetValueOrDefault(item.Id) - reversalMap.GetValueOrDefault(item.Id);
            return new CompanyProjectRowDto(
                item.Id,
                item.ProjectNumber,
                item.Name,
                StageLabel(item.Stage),
                shareByProject.GetValueOrDefault(item.Id),
                receivableMap.GetValueOrDefault(item.Id),
                collected,
                payableMap.GetValueOrDefault(item.Id),
                paid);
        }).ToArray();
    }

    public async Task<IReadOnlyList<CompanyContractRowDto>> ListCompanyContractsAsync(CompanyActor actor, Guid companyId, Guid? projectId, int take, CancellationToken cancellationToken)
    {
        await EnsureAccessAsync(actor, companyId, cancellationToken);
        take = NormalizeTake(take, 50);
        var query = db.ContractLegalEntityAllocations.AsNoTracking()
            .Where(item => item.LegalEntityId == companyId);
        if (projectId.HasValue)
        {
            query = query.Where(item => item.Contract.ProjectId == projectId.Value);
        }

        var contractRows = await query
            .Select(item => new
            {
                item.ContractId,
                item.Contract.ProjectId,
                item.Contract.ContractNumber,
                item.Contract.Name,
                item.Contract.TotalAmount,
                item.Amount,
                item.Percentage,
                item.Contract.IsActive,
                item.Contract.UpdatedAt
            })
            .ToListAsync(cancellationToken);
        var rows = contractRows
            .OrderByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.ContractNumber)
            .Take(take)
            .ToList();

        return rows.Select(item => new CompanyContractRowDto(
            item.ContractId,
            item.ProjectId,
            item.ContractNumber,
            item.Name,
            item.TotalAmount,
            item.Amount ?? item.TotalAmount * (item.Percentage ?? 0m) / 100m,
            item.Percentage,
            item.IsActive)).ToArray();
    }

    public async Task<IReadOnlyList<CompanyCollectionRowDto>> ListCompanyCollectionsAsync(CompanyActor actor, Guid companyId, int take, CancellationToken cancellationToken)
    {
        await EnsureAccessAsync(actor, companyId, cancellationToken);
        take = NormalizeTake(take, 50);
        return await db.CollectionEntries.AsNoTracking()
            .Where(item => item.LegalEntityId == companyId)
            .OrderByDescending(item => item.CollectionDate)
            .ThenByDescending(item => item.Id)
            .Take(take)
            .Select(item => new CompanyCollectionRowDto(
                item.Id,
                item.CollectionDate,
                item.ProjectId,
                item.Project.ProjectNumber,
                item.Project.Name,
                item.Notes ?? item.Project.Name,
                item.AccountId,
                item.Account.AccountName,
                item.Account.IsActive,
                item.Amount))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CompanyPaymentRowDto>> ListCompanyPaymentsAsync(CompanyActor actor, Guid companyId, int take, CancellationToken cancellationToken)
    {
        await EnsureAccessAsync(actor, companyId, cancellationToken);
        take = NormalizeTake(take, 50);
        return await db.PaymentEntries.AsNoTracking()
            .Where(item => item.LegalEntityId == companyId)
            .OrderByDescending(item => item.PaymentDate)
            .ThenByDescending(item => item.Id)
            .Take(take)
            .Select(item => new CompanyPaymentRowDto(
                item.Id,
                item.PaymentDate,
                item.ProjectId,
                item.Project.ProjectNumber,
                item.Project.Name,
                item.Notes ?? item.Project.Name,
                item.AccountId,
                item.Account.AccountName,
                item.Account.IsActive,
                item.Amount))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CompanyInvoiceRowDto>> ListCompanyInvoicesAsync(CompanyActor actor, Guid companyId, int take, CancellationToken cancellationToken)
    {
        await EnsureAccessAsync(actor, companyId, cancellationToken);
        take = NormalizeTake(take, 50);
        return await db.InvoiceEntries.AsNoTracking()
            .Where(item => item.LegalEntityId == companyId)
            .OrderByDescending(item => item.InvoiceDate)
            .ThenByDescending(item => item.Id)
            .Take(take)
            .Select(item => new CompanyInvoiceRowDto(
                item.Id,
                item.Direction == InvoiceDirection.Output ? "销项" : "进项",
                item.InvoiceNumber,
                item.InvoiceDate,
                item.ProjectId,
                item.Project.ProjectNumber,
                item.Project.Name,
                item.LegalEntity.Name,
                item.GrossAmount))
            .ToListAsync(cancellationToken);
    }
    public async Task<CompanyDashboardDto> GetDashboardAsync(CompanyActor actor, Guid? companyId, CancellationToken cancellationToken)
    {
        var authorizedIds = await AuthorizedCompanies(actor).Where(item => !companyId.HasValue || item.Id == companyId)
            .Select(item => item.Id).ToListAsync(cancellationToken);
        if (companyId.HasValue && authorizedIds.Count == 0)
        {
            throw new KeyNotFoundException("自有公司不存在或无权访问。");
        }
        var ids = authorizedIds.ToHashSet();
        var contractRows = await db.ContractLegalEntityAllocations.AsNoTracking().Where(item => ids.Contains(item.LegalEntityId))
            .Select(item => new { item.Amount, item.Percentage, item.Contract.TotalAmount }).ToListAsync(cancellationToken);
        var lineRows = await db.ContractLineItemLegalEntityAllocations.AsNoTracking().Where(item => ids.Contains(item.LegalEntityId))
            .Select(item => new { item.Amount, item.ContractLineItem.Contract.Project.Stage }).ToListAsync(cancellationToken);
        var receivables = await db.ReceivableEntries.AsNoTracking().Where(item => ids.Contains(item.LegalEntityId) && !item.IsVoided).Select(item => item.Amount).ToListAsync(cancellationToken);
        var collections = await db.CollectionEntries.AsNoTracking().Where(item => ids.Contains(item.LegalEntityId)).Select(item => item.Amount).ToListAsync(cancellationToken);
        var refunds = await db.RefundOrReversalEntries.AsNoTracking().Where(item =>
                (item.Receivable != null && ids.Contains(item.Receivable.LegalEntityId)) ||
                (item.Collection != null && ids.Contains(item.Collection.LegalEntityId)))
            .Select(item => item.Amount).ToListAsync(cancellationToken);
        var payables = await db.PayableEntries.AsNoTracking().Where(item => ids.Contains(item.LegalEntityId) && !item.IsVoided).Select(item => item.Amount).ToListAsync(cancellationToken);
        var payments = await db.PaymentEntries.AsNoTracking().Where(item => ids.Contains(item.LegalEntityId)).Select(item => item.Amount).ToListAsync(cancellationToken);
        var reversals = await db.PaymentReversalEntries.AsNoTracking().Where(item => ids.Contains(item.Payment.LegalEntityId)).Select(item => item.Amount).ToListAsync(cancellationToken);
        var invoices = await db.InvoiceEntries.AsNoTracking().Where(item => ids.Contains(item.LegalEntityId) && item.Status == InvoiceStatus.IssuedOrReceived)
            .Select(item => new { item.Direction, item.GrossAmount }).ToListAsync(cancellationToken);
        var payroll = await db.PayrollCostAllocations.AsNoTracking().Where(item => ids.Contains(item.LegalEntityId)).Select(item => item.Amount).ToListAsync(cancellationToken);
        var accounts = await db.FinancialAccounts.AsNoTracking().Where(item => ids.Contains(item.LegalEntityId) && item.IsActive)
            .Select(item => new { item.Id, item.OpeningBalance }).ToListAsync(cancellationToken);
        var accountIds = accounts.Select(item => item.Id).ToHashSet();
        var transactions = await db.AccountTransactions.AsNoTracking().Where(item => accountIds.Contains(item.AccountId))
            .Select(item => new { item.Direction, item.Amount }).ToListAsync(cancellationToken);
        var contractAmount = contractRows.Sum(item => item.Amount ?? item.TotalAmount * (item.Percentage ?? 0m) / 100m);
        var accountBalance = accounts.Sum(item => item.OpeningBalance)
            + transactions.Where(item => item.Direction == AccountTransactionDirection.Inflow).Sum(item => item.Amount)
            - transactions.Where(item => item.Direction == AccountTransactionDirection.Outflow).Sum(item => item.Amount);
        return new CompanyDashboardDto(
            ids.Count,
            contractAmount,
            lineRows.Where(item => item.Stage != ProjectStage.PartiallySettled && item.Stage != ProjectStage.SettledArchived).Sum(item => item.Amount),
            lineRows.Where(item => item.Stage == ProjectStage.PartiallySettled || item.Stage == ProjectStage.SettledArchived).Sum(item => item.Amount),
            receivables.Sum(),
            collections.Sum() - refunds.Sum(),
            payables.Sum(),
            payments.Sum() - reversals.Sum(),
            invoices.Where(item => item.Direction == InvoiceDirection.Output).Sum(item => item.GrossAmount),
            invoices.Where(item => item.Direction == InvoiceDirection.Input).Sum(item => item.GrossAmount),
            payroll.Sum(),
            0m,
            accountBalance,
            DateTimeOffset.UtcNow);
    }

    private async Task<HashSet<Guid>> GetCompanyProjectIdsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var fromOwnership = await db.ProjectLegalEntities.AsNoTracking()
            .Where(item => item.LegalEntityId == companyId)
            .Select(item => item.ProjectId)
            .ToListAsync(cancellationToken);
        var fromContracts = await db.ContractLegalEntityAllocations.AsNoTracking()
            .Where(item => item.LegalEntityId == companyId)
            .Select(item => item.Contract.ProjectId)
            .ToListAsync(cancellationToken);
        return fromOwnership.Concat(fromContracts).ToHashSet();
    }

    
    private static string StageLabel(ProjectStage stage) => stage switch
    {
        ProjectStage.AwaitingMobilization => "待进场",
        ProjectStage.UnderConstruction => "施工中",
        ProjectStage.Suspended => "停工中",
        ProjectStage.CompletedUnsettled => "已完工未结算",
        ProjectStage.PartiallySettled => "部分结算",
        ProjectStage.SettledArchived => "已结算归档",
        _ => stage.ToString()
    };
    private static int NormalizeTake(int take, int fallback) => take <= 0 ? fallback : Math.Min(take, 200);

    private IQueryable<LegalEntity> AuthorizedCompanies(CompanyActor actor)
    {
        var query = db.LegalEntities.AsQueryable();
        if (!actor.CanAccessAllCompanies)
        {
            var ids = actor.AccessibleCompanyIds.ToHashSet();
            query = query.Where(item => ids.Contains(item.Id));
        }
        return query;
    }

    private async Task EnsureAccessAsync(CompanyActor actor, Guid id, CancellationToken cancellationToken)
    {
        if (!await AuthorizedCompanies(actor).AnyAsync(item => item.Id == id, cancellationToken))
        {
            throw new KeyNotFoundException("自有公司不存在或无权访问。");
        }
    }

    private static void EnsureManage(CompanyActor actor)
    {
        if (!actor.CanManage) throw new UnauthorizedAccessException("当前用户没有自有公司管理权限。");
    }

    private async Task<CompanyDetailsDto> ToDetailsAsync(LegalEntity entity, CancellationToken cancellationToken)
    {
        var accounts = await db.FinancialAccounts.AsNoTracking().Where(item => item.LegalEntityId == entity.Id).OrderBy(item => item.AccountName).ToListAsync(cancellationToken);
        var certificates = await db.CompanyCertificates.AsNoTracking().Where(item => item.LegalEntityId == entity.Id && !item.IsDeleted).OrderBy(item => item.ExpiresOn).ToListAsync(cancellationToken);
        return new CompanyDetailsDto(entity.Id, entity.Code, entity.Name, entity.ShortName, entity.CompanyCategoryId,
            entity.CompanyCategory?.Name, entity.LegalRepresentative, entity.UnifiedSocialCreditCode,
            entity.RegisteredAddress, entity.BusinessAddress, entity.Phone, entity.InvoiceTitle, entity.Notes,
            entity.IsActive, entity.ConcurrencyStamp, accounts.Select(ToAccount).ToArray(), certificates.Select(ToCertificate).ToArray());
    }

    private static CompanyAccountDto ToAccount(FinancialAccount account) => new(account.Id, account.AccountName, account.AccountNumber,
        account.BankName, account.AccountType.ToString(), account.OpeningBalance, account.IsDefaultCollection,
        account.IsDefaultPayment, account.IsDefaultInvoice, account.IsActive, account.Notes, account.ConcurrencyStamp);

    private static CompanyCertificateDto ToCertificate(CompanyCertificate item) => new(item.Id, item.CertificateType,
        item.CertificateNumber, item.IssuedOn, item.ExpiresOn, item.AttachmentId, item.Notes);

    private static object Snapshot(LegalEntity item) => new
    {
        item.Code, item.Name, item.ShortName, item.CompanyCategoryId, item.LegalRepresentative,
        item.UnifiedSocialCreditCode, item.RegisteredAddress, item.BusinessAddress, item.Phone,
        item.InvoiceTitle, item.Notes, item.IsActive, item.ConcurrencyStamp
    };

    private static object Snapshot(FinancialAccount item) => new
    {
        item.LegalEntityId, item.AccountName, item.AccountNumber, item.BankName, item.AccountType,
        item.OpeningBalance, item.Notes, item.IsDefaultCollection, item.IsDefaultPayment,
        item.IsDefaultInvoice, item.IsActive, item.ConcurrencyStamp
    };

    private static string Required(string? value, string parameterName) => string.IsNullOrWhiteSpace(value)
        ? throw new ArgumentException("值不能为空。", parameterName)
        : value.Trim();

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}



