using System.Security.Claims;
using EngineeringManager.Application.EmployeeLedger;
using EngineeringManager.Application.Employees;
using EngineeringManager.Application.Payroll;
using EngineeringManager.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EngineeringManager.Tests.Web;

public sealed class EmployeePayrollAuthorizationTests
{
    [Theory]
    [InlineData("SystemAdministrator")]
    [InlineData("ApplicationAdministrator")]
    [InlineData("Finance")]
    [InlineData("ProjectManager")]
    [InlineData("QueryOnly")]
    public async Task AuthorizedRolesCanReadEmployeeList(string role)
    {
        await using var factory = CreateFactory(role);
        using var client = factory.CreateClient();

        (await client.GetAsync("/Employees")).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("SystemAdministrator")]
    [InlineData("ApplicationAdministrator")]
    public async Task AdministratorsCanCreateEmployees(string role)
    {
        await using var factory = CreateFactory(role);
        using var client = factory.CreateClient();

        (await client.GetAsync("/Employees/Create")).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Finance")]
    [InlineData("ProjectManager")]
    [InlineData("QueryOnly")]
    [InlineData("SiteStaff")]
    public async Task NonAdministratorsCannotCreateEmployees(string role)
    {
        await using var factory = CreateFactory(role);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await client.GetAsync("/Employees/Create")).StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("SystemAdministrator")]
    [InlineData("ApplicationAdministrator")]
    [InlineData("Finance")]
    [InlineData("QueryOnly")]
    public async Task FinanceReadersCanOpenPayrollAndEmployeeLedger(string role)
    {
        await using var factory = CreateFactory(role);
        using var client = factory.CreateClient();

        (await client.GetAsync("/Payroll")).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        (await client.GetAsync("/EmployeeLedger")).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("ProjectManager")]
    [InlineData("SiteStaff")]
    public async Task ProjectAndSiteRolesCannotOpenPayroll(string role)
    {
        await using var factory = CreateFactory(role);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await client.GetAsync("/Payroll")).StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    private static WebApplicationFactory<Program> CreateFactory(string role) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = EmployeePayrollTestHandler.Scheme;
                    options.DefaultChallengeScheme = EmployeePayrollTestHandler.Scheme;
                }).AddScheme<AuthenticationSchemeOptions, EmployeePayrollTestHandler>(EmployeePayrollTestHandler.Scheme, _ => { });
                services.RemoveAll<IEmployeeService>();
                services.RemoveAll<IPayrollService>();
                services.RemoveAll<IEmployeeLedgerService>();
                services.AddSingleton<IEmployeeService, FakeEmployeeService>();
                services.AddSingleton<IPayrollService, FakePayrollService>();
                services.AddSingleton<IEmployeeLedgerService, FakeEmployeeLedgerService>();
            });
            builder.UseSetting(EmployeePayrollTestHandler.RoleSetting, role);
        });

    private sealed class FakeEmployeeService : IEmployeeService
    {
        public Task<EmployeeDto> CreateAsync(CreateEmployeeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmployeeDto> CopyAsync(CopyEmployeeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmployeeDto> UpdateAsync(string userId, UpdateEmployeeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmployeeAffiliationDto> AddAffiliationAsync(CreateEmployeeAffiliationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<EmployeeDto>> ListAsync(string? search, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EmployeeDto>>([]);
        public Task<EmployeeDto?> GetAsync(Guid employeeId, CancellationToken cancellationToken) => Task.FromResult<EmployeeDto?>(null);
    }

    private sealed class FakePayrollService : IPayrollService
    {
        public Task<PayrollBatchDto> CreateBatchAsync(CreatePayrollBatchRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PayrollItemDto> AddItemAsync(CreatePayrollItemRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> RecordPaymentAsync(RecordPayrollPaymentRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PayrollBatchSummaryDto> GetBatchSummaryAsync(Guid batchId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<PayrollBatchDto>> ListBatchesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PayrollBatchDto>>([]);
        public Task<PayrollOverviewDto> GetOverviewAsync(CancellationToken cancellationToken) => Task.FromResult(new PayrollOverviewDto(0m, 0m, 0m, 0m, 0m, false, false, []));
    }

    private sealed class FakeEmployeeLedgerService : IEmployeeLedgerService
    {
        public Task<Guid> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> RecordExpensePaymentAsync(RecordExpensePaymentRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> RecordAdvanceAsync(RecordEmployeeAdvanceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> CreateOtherPayableAsync(CreateEmployeeOtherPayableRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> RecordOtherPaymentAsync(RecordEmployeeOtherPaymentRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmployeeLedgerSummaryDto> GetEmployeeSummaryAsync(Guid employeeId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmployeeLedgerOverviewDto> GetOverviewAsync(CancellationToken cancellationToken) => Task.FromResult(new EmployeeLedgerOverviewDto(0m, 0m, 0m, 0m, 0m, 0m, 0m, false, []));
    }

    private sealed class EmployeePayrollTestHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "EmployeePayrollTest";
        public const string RoleSetting = "EmployeePayrollTestAuth:Role";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Context.RequestServices.GetRequiredService<IConfiguration>()[RoleSetting];
            var identity = new ClaimsIdentity(Scheme, ClaimTypes.Name, ClaimTypes.Role);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "employee-payroll-user"));
            identity.AddClaim(new Claim(ClaimTypes.Name, "员工工资测试用户"));
            identity.AddClaim(new Claim(ClaimTypes.Role, role!));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}
