using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.Files;
using EngineeringManager.Application.Organization;
using EngineeringManager.Infrastructure.Organization;
using EngineeringManager.Application.Users;
using EngineeringManager.Infrastructure.Users;
using Microsoft.AspNetCore.Identity;
using EngineeringManager.Infrastructure.Identity;
using EngineeringManager.Application.Projects;
using EngineeringManager.Infrastructure.Projects;
using EngineeringManager.Application.Partners;
using EngineeringManager.Infrastructure.Partners;
using EngineeringManager.Application.StageResults;
using EngineeringManager.Infrastructure.StageResults;
using EngineeringManager.Application.Finance;
using EngineeringManager.Infrastructure.Finance;
using EngineeringManager.Application.Employees;
using EngineeringManager.Infrastructure.Employees;
using EngineeringManager.Application.Payroll;
using EngineeringManager.Infrastructure.Payroll;
using EngineeringManager.Application.EmployeeLedger;
using EngineeringManager.Infrastructure.EmployeeLedger;
using EngineeringManager.Application.DataExchange;
using EngineeringManager.Infrastructure.DataExchange;
using EngineeringManager.Application.Backups;
using EngineeringManager.Infrastructure.Backups;
using EngineeringManager.Application.Reminders;
using EngineeringManager.Infrastructure.Reminders;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EngineeringManager.Web;

public sealed class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        builder.Services
            .AddDefaultIdentity<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        builder.Services.AddRazorPages();
        builder.Services.AddScoped<IOrganizationService, OrganizationService>();
        builder.Services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        builder.Services.AddScoped<IProjectService, ProjectService>();
        builder.Services.AddScoped<IBusinessPartnerService, BusinessPartnerService>();
        builder.Services.AddScoped<IStageResultService, StageResultService>();
        builder.Services.AddScoped<IFinanceLedgerService, FinanceLedgerService>();
        builder.Services.AddScoped<IEmployeeService, EmployeeService>();
        builder.Services.AddScoped<IPayrollService, PayrollService>();
        builder.Services.AddScoped<IEmployeeLedgerService, EmployeeLedgerService>();
        builder.Services.AddScoped<IExportService, ExportService>();
        builder.Services.AddScoped<IImportService, ImportService>();
        builder.Services.AddSingleton<IDatabaseBackupExecutor>(_ => new SqlServerBackupExecutor(connectionString));
        builder.Services.AddScoped<IBackupService>(services => new BackupService(
            services.GetRequiredService<ApplicationDbContext>(),
            services.GetRequiredService<IDatabaseBackupExecutor>(),
            Path.Combine(builder.Environment.ContentRootPath, "App_Data", "attachments"),
            Path.Combine(builder.Environment.ContentRootPath, "App_Data", "backups")));
        builder.Services.AddScoped<IReminderService, ReminderService>();
        builder.Services.AddSingleton<IFileStore>(_ =>
            new LocalFileStore(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "attachments")));
        builder.Services
            .AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
            .AddDbContextCheck<ApplicationDbContext>("database", tags: ["ready"]);

        var app = builder.Build();

        Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data", "attachments"));
        Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data", "backups"));
        Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data", "logs"));

        if (app.Configuration.GetValue<bool>("Identity:SeedRoles"))
        {
            await IdentitySeed.EnsureRolesAsync(app.Services);
        }

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapStaticAssets();
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live")
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready")
        });
        app.MapRazorPages()
            .WithStaticAssets();

        await app.RunAsync();
    }
}
