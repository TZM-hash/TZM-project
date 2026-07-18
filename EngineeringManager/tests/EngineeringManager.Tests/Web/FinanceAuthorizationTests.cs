using System.Security.Claims;
using EngineeringManager.Application.DataViews;
using EngineeringManager.Application.Finance;
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

public sealed class FinanceAuthorizationTests
{
    [Theory]
    [InlineData("SystemAdministrator")]
    [InlineData("ApplicationAdministrator")]
    [InlineData("Finance")]
    [InlineData("QueryOnly")]
    public async Task FinanceOverviewRedirectsToUnifiedProjectManagement(string role)
    {
        await using var factory = CreateFactory(role);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/Finance");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Redirect);
        response.Headers.Location?.OriginalString.Should().Be("/Projects");
    }

    [Theory]
    [InlineData("SystemAdministrator")]
    [InlineData("ApplicationAdministrator")]
    [InlineData("Finance")]
    public async Task FinanceManagersCanOpenAccountsAndEntryPages(string role)
    {
        await using var factory = CreateFactory(role);
        using var client = factory.CreateClient();

        (await client.GetAsync("/Finance/Accounts")).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        (await client.GetAsync("/Finance/Entries/Create")).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("QueryOnly")]
    [InlineData("ProjectManager")]
    [InlineData("SiteStaff")]
    public async Task NonFinanceManagersCannotOpenFinancialEntry(string role)
    {
        await using var factory = CreateFactory(role);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await client.GetAsync("/Finance/Entries/Create")).StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    private static WebApplicationFactory<Program> CreateFactory(string role) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = FinanceTestHandler.Scheme;
                    options.DefaultChallengeScheme = FinanceTestHandler.Scheme;
                }).AddScheme<AuthenticationSchemeOptions, FinanceTestHandler>(FinanceTestHandler.Scheme, _ => { });
                services.RemoveAll<IFinanceLedgerService>();
                services.AddSingleton<IFinanceLedgerService, FakeFinanceLedgerService>();
                services.RemoveAll<ISavedDataViewService>();
                services.AddSingleton<ISavedDataViewService, EmptySavedViewService>();
            });
            builder.UseSetting(FinanceTestHandler.RoleSetting, role);
        });

    private sealed class FakeFinanceLedgerService : IFinanceLedgerService
    {
        public Task<Guid> CreateAccountAsync(CreateFinancialAccountRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<FinancialAccountDto>> ListAccountsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<FinancialAccountDto>>([]);
        public Task<IReadOnlyList<ProjectFinanceListItemDto>> ListProjectSummariesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProjectFinanceListItemDto>>([]);
        public Task<FinanceOverviewDto> GetOverviewAsync(CancellationToken cancellationToken) => Task.FromResult(new FinanceOverviewDto([], new FinanceProjectSummaryDto(Guid.Empty, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, false, false)));
        public Task<FinanceOverviewPageDto> SearchOverviewAsync(FinanceOverviewQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new FinanceOverviewPageDto([], new FinanceProjectSummaryDto(Guid.Empty, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, false, false), 1, 20, 0, 1, []));
        public Task<FinanceEntryOptionsDto> GetEntryOptionsAsync(CancellationToken cancellationToken) => Task.FromResult(new FinanceEntryOptionsDto([], [], [], [], [], [], [], [], []));
        public Task<Guid> AddReceivableAsync(CreateReceivableRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> RecordCollectionAsync(RecordCollectionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> RecordRefundAsync(RecordRefundRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> AddPayableAsync(CreatePayableRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> AddDeductionAsync(CreateDeductionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> RecordPaymentReversalAsync(RecordPaymentReversalRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> TransferAsync(CreateAccountTransferRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> AddInvoiceAsync(CreateInvoiceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateReceivableAsync(FinanceRecordActor actor, UpdateReceivableRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateCollectionAsync(FinanceRecordActor actor, UpdateCollectionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateInvoiceAsync(FinanceRecordActor actor, UpdateInvoiceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdatePayableAsync(FinanceRecordActor actor, UpdatePayableRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdatePaymentAsync(FinanceRecordActor actor, UpdatePaymentRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FinanceProjectSummaryDto> GetSummaryAsync(FinanceSummaryFilter filter, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FinanceProjectSummaryDto> GetProjectSummaryAsync(Guid projectId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class EmptySavedViewService : ISavedDataViewService
    {
        public Task<IReadOnlyList<SavedDataViewDto>> ListAsync(string userId, DataViewDefinition definition, CancellationToken token) => Task.FromResult<IReadOnlyList<SavedDataViewDto>>([]);
        public Task<SavedDataViewDto> SaveAsync(string userId, SaveDataViewRequest request, DataViewDefinition definition, CancellationToken token) => throw new NotSupportedException();
        public Task DeleteAsync(string userId, Guid id, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class FinanceTestHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "FinanceTest";
        public const string RoleSetting = "FinanceTestAuth:Role";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Context.RequestServices.GetRequiredService<IConfiguration>()[RoleSetting];
            var identity = new ClaimsIdentity(Scheme, ClaimTypes.Name, ClaimTypes.Role);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "finance-test-user"));
            identity.AddClaim(new Claim(ClaimTypes.Name, "财务测试用户"));
            identity.AddClaim(new Claim(ClaimTypes.Role, role!));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}
