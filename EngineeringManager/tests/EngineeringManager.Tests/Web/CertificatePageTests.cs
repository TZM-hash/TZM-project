using System.Net;
using System.Security.Claims;
using EngineeringManager.Application.Certificates;
using EngineeringManager.Application.Companies;
using EngineeringManager.Application.Employees;
using EngineeringManager.Domain.Certificates;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EngineeringManager.Tests.Web;

public sealed class CertificatePageTests
{
    [Theory]
    [InlineData("/Employees/Certificates")]
    [InlineData("/Companies/Certificates")]
    public async Task AnonymousUserIsRedirected(string url)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task AdministratorSeesBothCertificateWorkbenchesAndManageActions()
    {
        await using var factory = CreateFactory("ApplicationAdministrator");
        using var client = factory.CreateClient();
        var employeeHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Employees/Certificates"));
        var companyHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Companies/Certificates"));
        employeeHtml.Should().Contain("employee-certificates-table").And.Contain("新增员工证书").And.Contain("建造师证");
        companyHtml.Should().Contain("company-certificates-table").And.Contain("新增公司证书").And.Contain("安全生产许可证");
    }

    [Fact]
    public async Task QueryOnlyCanReadButCannotOpenEditPage()
    {
        await using var factory = CreateFactory("QueryOnly");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.GetAsync("/Employees/Certificates")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/Employees/Certificates/Edit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static WebApplicationFactory<Program> CreateFactory(string role) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseSetting(TestAuthHandler.RoleSetting, role);
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options => { options.DefaultAuthenticateScheme = TestAuthHandler.Scheme; options.DefaultChallengeScheme = TestAuthHandler.Scheme; }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });
            services.RemoveAll<IEmployeeCertificateService>(); services.AddSingleton<IEmployeeCertificateService, FakeEmployeeCertificateService>();
            services.RemoveAll<ICompanyCertificateService>(); services.AddSingleton<ICompanyCertificateService, FakeCompanyCertificateService>();
            services.RemoveAll<IEmployeeService>(); services.AddSingleton<IEmployeeService, FakeEmployeeService>();
            services.RemoveAll<ICompanyManagementService>(); services.AddSingleton<ICompanyManagementService, FakeCompanyService>();
            services.RemoveAll<ICompanyActorService>(); services.AddSingleton<ICompanyActorService, FakeCompanyActorService>();
        });
    });

    private sealed class FakeEmployeeCertificateService : IEmployeeCertificateService
    {
        public Task<IReadOnlyList<EmployeeCertificateDto>> ListAsync(CertificateFilter filter, DateOnly today, CancellationToken token) => Task.FromResult<IReadOnlyList<EmployeeCertificateDto>>([new(Guid.NewGuid(), FakeEmployeeService.Id, "E-001", "测试员工", "建造师证", "JZS-01", "建筑工程一级", "住建部门", new DateOnly(2020, 1, 1), today.AddMonths(2), null, null, null, CertificateExpiryState.Warning, Guid.NewGuid())]);
        public Task<EmployeeCertificateDto> GetAsync(Guid id, DateOnly today, CancellationToken token) => throw new NotSupportedException();
        public Task<EmployeeCertificateDto> SaveAsync(string userId, bool canManage, SaveEmployeeCertificateRequest request, DateOnly today, CancellationToken token) => throw new NotSupportedException();
        public Task DeleteAsync(string userId, bool canManage, Guid id, Guid concurrencyStamp, string reason, CancellationToken token) => throw new NotSupportedException();
        public Task<CertificateFileDto> DownloadAttachmentAsync(Guid id, CancellationToken token) => throw new NotSupportedException();
    }
    private sealed class FakeCompanyCertificateService : ICompanyCertificateService
    {
        public Task<IReadOnlyList<CompanyCertificateItemDto>> ListAsync(CompanyActor actor, CertificateFilter filter, DateOnly today, CancellationToken token) => Task.FromResult<IReadOnlyList<CompanyCertificateItemDto>>([new(Guid.NewGuid(), FakeCompanyService.Id, "C-001", "测试公司", "安全生产许可证", "AQ-01", null, "住建部门", null, today.AddMonths(1), null, null, null, CertificateExpiryState.Critical, Guid.NewGuid())]);
        public Task<CompanyCertificateItemDto> GetAsync(CompanyActor actor, Guid id, DateOnly today, CancellationToken token) => throw new NotSupportedException();
        public Task<CompanyCertificateItemDto> SaveAsync(CompanyActor actor, SaveCompanyCertificateItemRequest request, DateOnly today, CancellationToken token) => throw new NotSupportedException();
        public Task DeleteAsync(CompanyActor actor, Guid id, Guid concurrencyStamp, string reason, CancellationToken token) => throw new NotSupportedException();
        public Task<CertificateFileDto> DownloadAttachmentAsync(CompanyActor actor, Guid id, CancellationToken token) => throw new NotSupportedException();
    }
    private sealed class FakeEmployeeService : IEmployeeService
    {
        public static readonly Guid Id = Guid.Parse("10000000-0000-0000-0000-000000000001");
        public Task<IReadOnlyList<EmployeeDto>> ListAsync(string? search, CancellationToken token) => Task.FromResult<IReadOnlyList<EmployeeDto>>([new(Id, "E-001", "测试员工", EmployeeType.Formal, null, "项目经理", null, null, null, null, null, true, [])]);
        public Task<EmployeeDto?> GetAsync(Guid employeeId, CancellationToken token) => throw new NotSupportedException();
        public Task<EmployeeDto> CreateAsync(CreateEmployeeRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<EmployeeDto> CopyAsync(CopyEmployeeRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<EmployeeDto> UpdateAsync(string userId, UpdateEmployeeRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<EmployeeAffiliationDto> AddAffiliationAsync(CreateEmployeeAffiliationRequest request, CancellationToken token) => throw new NotSupportedException();
    }
    private sealed class FakeCompanyService : ICompanyManagementService
    {
        public static readonly Guid Id = Guid.Parse("20000000-0000-0000-0000-000000000001");
        public Task<IReadOnlyList<CompanyListItemDto>> ListAsync(CompanyActor actor, CancellationToken token) => Task.FromResult<IReadOnlyList<CompanyListItemDto>>([new(Id, "C-001", "测试公司", "测试", null, null, true)]);
        public Task<CompanyDetailsDto> GetAsync(CompanyActor actor, Guid id, CancellationToken token) => throw new NotSupportedException();
        public Task<CompanyDetailsDto> SaveCompanyAsync(CompanyActor actor, SaveCompanyRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<SaveCompanyRequest> PrepareCopyAsync(CompanyActor actor, Guid sourceId, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<CompanyCategoryDto>> ListCategoriesAsync(CancellationToken token) => throw new NotSupportedException();
        public Task<CompanyCategoryDto> SaveCategoryAsync(CompanyActor actor, SaveCompanyCategoryRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<CompanyAccountDto> SaveAccountAsync(CompanyActor actor, SaveCompanyAccountRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<CompanyCertificateDto> SaveCertificateAsync(CompanyActor actor, SaveCompanyCertificateRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<CompanyDashboardDto> GetDashboardAsync(CompanyActor actor, Guid? companyId, CancellationToken token) => throw new NotSupportedException();
    }
    private sealed class FakeCompanyActorService : ICompanyActorService { public Task<CompanyActor> ResolveAsync(string userId, IReadOnlyCollection<string> roles, CancellationToken token) => Task.FromResult(CompanyActor.Administrator(userId)); }
    private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "CertificateTest"; public const string RoleSetting = "CertificateTest:Role";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() { var role = Context.RequestServices.GetRequiredService<IConfiguration>()[RoleSetting]; var identity = new ClaimsIdentity(Scheme, ClaimTypes.Name, ClaimTypes.Role); identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "certificate-test")); identity.AddClaim(new Claim(ClaimTypes.Name, "证书测试用户")); identity.AddClaim(new Claim(ClaimTypes.Role, role!)); return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme))); }
    }
}
