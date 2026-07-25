using EngineeringManager.Application.Certificates;
using EngineeringManager.Application.Companies;
using EngineeringManager.Application.Employees;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Security;
using EngineeringManager.Web.Presentation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace EngineeringManager.Web.Pages.Companies;

[Authorize(Roles = SystemRoles.SystemAdministrator + "," + SystemRoles.ApplicationAdministrator + "," + SystemRoles.Finance + "," + SystemRoles.ProjectManager + "," + SystemRoles.QueryOnly)]
public sealed class DetailsModel(
    ICompanyManagementService companyService,
    ICompanyCertificateService certificateService,
    ICompanyActorService actorService,
    IEmployeeService employeeService) : CompanyPageModel(actorService)
{
    public CompanyDetailsDto Company { get; private set; } = null!;
    public CompanyDashboardDto Dashboard { get; private set; } = null!;
    public IReadOnlyList<CompanyCategoryDto> Categories { get; private set; } = [];
    public IReadOnlyList<CompanyListItemDto> CompanyOptions { get; private set; } = [];
    public CompanyWorkspaceSummaryDto? WorkspaceSummary { get; private set; }
    public IReadOnlyList<CompanyActivityItemDto> RecentActivity { get; private set; } = [];
    public IReadOnlyList<CompanyProjectRowDto> Projects { get; private set; } = [];
    public IReadOnlyList<CompanyContractRowDto> Contracts { get; private set; } = [];
    public IReadOnlyList<CompanyCollectionRowDto> Collections { get; private set; } = [];
    public IReadOnlyList<CompanyPaymentRowDto> Payments { get; private set; } = [];
    public IReadOnlyList<CompanyInvoiceRowDto> Invoices { get; private set; } = [];
    public IReadOnlyList<CompanyCertificateItemDto> Certificates { get; private set; } = [];
    public int EmployeeCount { get; private set; }
    public bool CanManage => User.IsInRole(SystemRoles.SystemAdministrator) || User.IsInRole(SystemRoles.ApplicationAdministrator);
    public bool QuickEditOpen { get; private set; }
    public bool AccountEditOpen { get; private set; }
    public Guid? CertificateEditId { get; private set; }
    public string ActiveTab => NormalizeTab(Tab);

    [BindProperty(SupportsGet = true)] public string? Tab { get; set; }
    [BindProperty(SupportsGet = true)] public string? ProjectSearch { get; set; }
    [BindProperty] public AccountInput Account { get; set; } = new();
    [BindProperty] public List<AccountRowInput> AccountRows { get; set; } = [];
    [BindProperty] public CertificateInput Certificate { get; set; } = new();
    [BindProperty] public IFormFile? CertificateAttachmentFile { get; set; }
    [BindProperty] public EditModel.InputModel QuickEdit { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await LoadAsync(id, true, cancellationToken);
            return Page();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnPostQuickEditAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        Tab = "profile";
        QuickEdit.Id = id;
        if (!ModelState.IsValid)
        {
            QuickEditOpen = true;
            await LoadAsync(id, false, cancellationToken);
            return Page();
        }
        try
        {
            var actor = await ResolveActorAsync(cancellationToken);
            await companyService.SaveCompanyAsync(actor, QuickEdit.ToRequest(), cancellationToken);
            return RedirectToPage(new { id, tab = "profile" });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            QuickEditOpen = true;
            await LoadAsync(id, false, cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAccountAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        Tab = "accounts";
        RemoveUnrelatedModelState($"{nameof(Account)}.");
        if (!TryValidateModel(Account, nameof(Account)))
        {
            await LoadAsync(id, true, cancellationToken);
            return Page();
        }
        try
        {
            var actor = await ResolveActorAsync(cancellationToken);
            await companyService.SaveAccountAsync(actor, new SaveCompanyAccountRequest(Account.Id, id, Account.Name, Account.Number, Account.BankName,
                Account.AccountType, Account.OpeningBalance, Account.DefaultCollection, Account.DefaultPayment, Account.DefaultInvoice, Account.IsActive,
                Account.ConcurrencyStamp, Account.Id.HasValue ? "修改公司账户" : "新增公司账户", Account.Notes), cancellationToken);
            return RedirectToPage(new { id, tab = "accounts" });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(id, true, cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAccountStatusAsync(Guid id, Guid accountId, Guid concurrencyStamp, bool isActive, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        Tab = "accounts";
        try
        {
            var actor = await ResolveActorAsync(cancellationToken);
            var company = await companyService.GetAsync(actor, id, cancellationToken);
            var account = company.Accounts.SingleOrDefault(item => item.Id == accountId)
                ?? throw new KeyNotFoundException("公司账户不存在。");
            await companyService.SaveAccountAsync(actor, new SaveCompanyAccountRequest(account.Id, id, account.AccountName, account.AccountNumber,
                account.BankName, (int)Enum.Parse<FinancialAccountType>(account.AccountType), account.OpeningBalance,
                isActive && account.IsDefaultCollection, isActive && account.IsDefaultPayment, isActive && account.IsDefaultInvoice,
                isActive, concurrencyStamp, isActive ? "启用公司账户" : "停用公司账户", account.Notes), cancellationToken);
            return RedirectToPage(new { id, tab = "accounts" });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(id, true, cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCertificateAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        Tab = "certificates";
        var certificatePrefix = $"{nameof(Certificate)}.";
        foreach (var key in ModelState.Keys.Where(key => !key.StartsWith(certificatePrefix, StringComparison.Ordinal)).ToArray())
        {
            ModelState.Remove(key);
        }
        if (!TryValidateModel(Certificate, nameof(Certificate)))
        {
            CertificateEditId = Certificate.Id;
            await LoadAsync(id, true, cancellationToken);
            return Page();
        }

        try
        {
            var actor = await ResolveActorAsync(cancellationToken);
            CompanyCertificateItemDto? existing = null;
            if (Certificate.Id.HasValue)
            {
                existing = await certificateService.GetAsync(actor, Certificate.Id.Value, DateOnly.FromDateTime(DateTime.Today), cancellationToken);
                if (existing.LegalEntityId != id) throw new KeyNotFoundException("公司证照不存在或无权访问。");
            }

            await certificateService.SaveAsync(actor, new SaveCompanyCertificateItemRequest(
                Certificate.Id,
                id,
                Certificate.Type,
                Certificate.Number,
                existing?.SpecialtyLevelScope,
                existing?.IssuingAuthority,
                Certificate.IssuedOn,
                Certificate.ExpiresOn,
                null,
                false,
                Certificate.Notes,
                Certificate.ConcurrencyStamp,
                Certificate.Reason), DateOnly.FromDateTime(DateTime.Today), cancellationToken);
            return RedirectToPage(new { id, tab = "certificates" });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception is DbUpdateConcurrencyException
                ? "数据已被他人更新，请刷新后重试。"
                : exception.Message);
            CertificateEditId = Certificate.Id;
            await LoadAsync(id, true, cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAccountsAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        Tab = "accounts";
        RemoveUnrelatedModelState($"{nameof(AccountRows)}[");
        if (AccountRows.Count == 0 || !TryValidateModel(AccountRows, nameof(AccountRows)))
        {
            if (AccountRows.Count == 0) ModelState.AddModelError(string.Empty, "没有可保存的公司账户。");
            AccountEditOpen = true;
            await LoadAsync(id, true, cancellationToken);
            return Page();
        }

        try
        {
            var actor = await ResolveActorAsync(cancellationToken);
            var requests = AccountRows.Select(account => new SaveCompanyAccountRequest(account.Id, id, account.Name, account.Number,
                    account.BankName, account.AccountType, account.OpeningBalance,
                    (account.DefaultPurpose & 1) != 0, (account.DefaultPurpose & 2) != 0, (account.DefaultPurpose & 4) != 0,
                    account.IsActive, account.ConcurrencyStamp, "批量修改公司账户", account.Notes)).ToList();
            await companyService.SaveAccountsAsync(actor, requests, cancellationToken);
            return RedirectToPage(new { id, tab = "accounts" });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            AccountEditOpen = true;
            await LoadAsync(id, true, cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCertificateAttachmentAsync(Guid id, Guid certificateId, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        Tab = "certificates";
        try
        {
            var file = CertificateAttachmentFile ?? throw new ArgumentException("请选择需要上传的附件。");
            var actor = await ResolveActorAsync(cancellationToken);
            var certificate = await GetCompanyCertificateAsync(actor, id, certificateId, cancellationToken);
            var attachment = await BuildAttachmentUploadAsync(file, cancellationToken);
            await certificateService.SaveAsync(actor, BuildAttachmentRequest(certificate, attachment, false, "上传公司证书附件"),
                DateOnly.FromDateTime(DateTime.Today), cancellationToken);
            return RedirectToPage(new { id, tab = "certificates" });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception is DbUpdateConcurrencyException
                ? "数据已被他人更新，请刷新后重试。"
                : exception.Message);
            await LoadAsync(id, true, cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnGetCertificateAttachmentAsync(
        Guid id,
        Guid certificateId,
        bool download,
        bool officePreview,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = await ResolveActorAsync(cancellationToken);
            var certificate = await GetCompanyCertificateAsync(actor, id, certificateId, cancellationToken);
            if (!certificate.AttachmentId.HasValue) return NotFound();
            var file = await certificateService.DownloadAttachmentAsync(actor, certificateId, cancellationToken);
            if (officePreview)
            {
                var html = OfficeAttachmentPreview.Create(file.OriginalFileName, file.Content)
                    ?? "<!doctype html><html lang=\"zh-CN\"><body><p>此 Office 文件无法直接预览，请下载后打开。</p></body></html>";
                return new ContentResult { Content = html, ContentType = "text/html; charset=utf-8" };
            }

            return download
                ? File(file.Content, file.ContentType, file.OriginalFileName)
                : File(file.Content, file.ContentType);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnPostDeleteCertificateAttachmentAsync(Guid id, Guid certificateId, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        Tab = "certificates";
        try
        {
            var actor = await ResolveActorAsync(cancellationToken);
            var certificate = await GetCompanyCertificateAsync(actor, id, certificateId, cancellationToken);
            if (!certificate.AttachmentId.HasValue) return NotFound();
            await certificateService.SaveAsync(actor, BuildAttachmentRequest(certificate, null, true, "删除公司证书附件"),
                DateOnly.FromDateTime(DateTime.Today), cancellationToken);
            return RedirectToPage(new { id, tab = "certificates" });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, exception is DbUpdateConcurrencyException
                ? "数据已被他人更新，请刷新后重试。"
                : exception.Message);
            await LoadAsync(id, true, cancellationToken);
            return Page();
        }
    }

    private async Task<CompanyCertificateItemDto> GetCompanyCertificateAsync(
        CompanyActor actor,
        Guid companyId,
        Guid certificateId,
        CancellationToken cancellationToken)
    {
        var certificate = await certificateService.GetAsync(actor, certificateId, DateOnly.FromDateTime(DateTime.Today), cancellationToken);
        if (certificate.LegalEntityId != companyId) throw new KeyNotFoundException("公司证照不存在或无权访问。");
        return certificate;
    }

    private static SaveCompanyCertificateItemRequest BuildAttachmentRequest(
        CompanyCertificateItemDto certificate,
        CertificateAttachmentUpload? attachment,
        bool removeAttachment,
        string reason) => new(
            certificate.Id,
            certificate.LegalEntityId,
            certificate.CertificateType,
            certificate.CertificateNumber,
            certificate.SpecialtyLevelScope,
            certificate.IssuingAuthority,
            certificate.IssuedOn,
            certificate.ExpiresOn,
            attachment,
            removeAttachment,
            certificate.Notes,
            certificate.ConcurrencyStamp,
            reason);

    private static async Task<CertificateAttachmentUpload> BuildAttachmentUploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length is 0 or > 20 * 1024 * 1024) throw new ArgumentException("附件不能为空且不能超过 20MB。");
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        return new CertificateAttachmentUpload(Path.GetFileName(file.FileName),
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            buffer.ToArray());
    }

    private async Task LoadAsync(Guid id, bool populateQuickEdit, CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);
        Company = await companyService.GetAsync(actor, id, cancellationToken);
        Dashboard = await companyService.GetDashboardAsync(actor, id, cancellationToken);
        CompanyOptions = await companyService.ListAsync(actor, cancellationToken);
        var employees = await employeeService.ListAsync(null, false, cancellationToken);
        EmployeeCount = CountActiveEmployees(employees, [id]);

        if (CanManage)
        {
            Categories = await companyService.ListCategoriesAsync(cancellationToken);
            if (populateQuickEdit) QuickEdit = EditModel.InputModel.From(Company);
            if (ActiveTab == "accounts" && AccountRows.Count == 0)
            {
                AccountRows = Company.Accounts.OrderByDescending(item => item.IsActive).ThenBy(item => item.AccountName)
                    .Select(AccountRowInput.From).ToList();
            }
        }

        switch (ActiveTab)
        {
            case "overview":
                WorkspaceSummary = await companyService.GetWorkspaceSummaryAsync(actor, id, cancellationToken);
                RecentActivity = await companyService.ListRecentActivityAsync(actor, id, 10, cancellationToken);
                break;
            case "certificates":
                Certificates = await certificateService.ListAsync(actor, new CertificateFilter(OwnerId: id), DateOnly.FromDateTime(DateTime.Today), cancellationToken);
                break;
            case "projects":
                Projects = await companyService.ListCompanyProjectsAsync(actor, id, ProjectSearch, 50, cancellationToken);
                Contracts = await companyService.ListCompanyContractsAsync(actor, id, null, 50, cancellationToken);
                break;
            case "finance":
                Collections = await companyService.ListCompanyCollectionsAsync(actor, id, 50, cancellationToken);
                Payments = await companyService.ListCompanyPaymentsAsync(actor, id, 50, cancellationToken);
                Invoices = await companyService.ListCompanyInvoicesAsync(actor, id, 50, cancellationToken);
                break;
        }
    }

    private static string NormalizeTab(string? tab) => tab?.Trim().ToLowerInvariant() switch
    {
        "profile" => "profile",
        "certificates" => "certificates",
        "accounts" => "accounts",
        "projects" => "projects",
        "finance" => "finance",
        _ => "overview"
    };

    private void RemoveUnrelatedModelState(string prefix)
    {
        foreach (var key in ModelState.Keys.Where(key => !key.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
        {
            ModelState.Remove(key);
        }
    }

    public sealed class AccountInput
    {
        public Guid? Id { get; set; }
        public Guid? ConcurrencyStamp { get; set; }
        [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
        public string? Number { get; set; }
        public string? BankName { get; set; }
        public string? Notes { get; set; }
        [Range(1, 3)]
        public int AccountType { get; set; } = (int)FinancialAccountType.Bank;
        public decimal OpeningBalance { get; set; }
        public bool DefaultCollection { get; set; }
        public bool DefaultPayment { get; set; }
        public bool DefaultInvoice { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public sealed class AccountRowInput
    {
        public Guid Id { get; set; }
        public Guid ConcurrencyStamp { get; set; }
        [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
        public string? Number { get; set; }
        public string? BankName { get; set; }
        public string? Notes { get; set; }
        [Range(1, 3)] public int AccountType { get; set; } = (int)FinancialAccountType.Bank;
        public decimal OpeningBalance { get; set; }
        [Range(0, 7)] public int DefaultPurpose { get; set; }
        public bool IsActive { get; set; } = true;

        public static AccountRowInput From(CompanyAccountDto account) => new()
        {
            Id = account.Id,
            ConcurrencyStamp = account.ConcurrencyStamp,
            Name = account.AccountName,
            Number = account.AccountNumber,
            BankName = account.BankName,
            Notes = account.Notes,
            AccountType = Enum.TryParse<FinancialAccountType>(account.AccountType, out var accountType)
                ? (int)accountType
                : (int)FinancialAccountType.Other,
            OpeningBalance = account.OpeningBalance,
            DefaultPurpose = (account.IsDefaultCollection ? 1 : 0)
                | (account.IsDefaultPayment ? 2 : 0)
                | (account.IsDefaultInvoice ? 4 : 0),
            IsActive = account.IsActive
        };
    }

    public sealed class CertificateInput
    {
        public Guid? Id { get; set; }
        public Guid? ConcurrencyStamp { get; set; }
        [Required, StringLength(100)] public string Type { get; set; } = string.Empty;
        [StringLength(100)]
        public string? Number { get; set; }
        public DateOnly? IssuedOn { get; set; }
        public DateOnly? ExpiresOn { get; set; }
        [StringLength(1000)]
        public string? Notes { get; set; }
        [Required, StringLength(500)] public string Reason { get; set; } = "维护公司证照";
    }
}
