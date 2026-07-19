using System.Text.Json;
using EngineeringManager.Application.Companies;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
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
            item.LegalRepresentative, item.IsActive, item.Notes)).ToListAsync(cancellationToken);
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
            item.LegalRepresentative, item.IsActive, item.Notes)).ToListAsync(cancellationToken);
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
        account.IsDefaultCollection = request.IsDefaultCollection;
        account.IsDefaultPayment = request.IsDefaultPayment;
        account.IsDefaultInvoice = request.IsDefaultInvoice;
        account.IsActive = request.IsActive;
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
            .Select(item => new { item.Amount, item.ContractLineItem.IsSettlementConfirmed }).ToListAsync(cancellationToken);
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
            lineRows.Where(item => !item.IsSettlementConfirmed).Sum(item => item.Amount),
            lineRows.Where(item => item.IsSettlementConfirmed).Sum(item => item.Amount),
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
        account.IsDefaultPayment, account.IsDefaultInvoice, account.IsActive, account.Notes);

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
