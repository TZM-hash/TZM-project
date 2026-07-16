using System.Text;
using EngineeringManager.Application.Development;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Development;

public sealed class DevelopmentSampleDataSeeder(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : IDevelopmentSampleDataSeeder
{
    public const string AdministratorUserName = "test-admin";
    private static int passwordSequence = Random.Shared.Next(1000, 9000);

    public static string GenerateTestPassword() => $"TestAdmin{Interlocked.Increment(ref passwordSequence) % 10000:0000}";

    public static void ValidateSafety(string environmentName, string databaseName)
    {
        if (!string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) || !databaseName.EndsWith("_Test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("样例数据只能写入 Development 环境中明确以 _Test 结尾的测试数据库。");
    }

    public async Task SeedAsync(string environmentName, string contentRootPath, CancellationToken token)
    {
        var databaseName = db.Database.GetDbConnection().Database;
        ValidateSafety(environmentName, databaseName);
        var password = GenerateTestPassword();
        var user = await userManager.FindByNameAsync(AdministratorUserName);
        if (user is null)
        {
            user = new ApplicationUser { UserName = AdministratorUserName, Email = "test-admin@local.invalid", DisplayName = "系统测试管理员", EmailConfirmed = true };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded) throw new InvalidOperationException(string.Join("；", result.Errors.Select(item => item.Description)));
        }
        else
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var result = await userManager.ResetPasswordAsync(user, resetToken, password);
            if (!result.Succeeded) throw new InvalidOperationException(string.Join("；", result.Errors.Select(item => item.Description)));
        }
        if (!await userManager.IsInRoleAsync(user, SystemRoles.SystemAdministrator)) await userManager.AddToRoleAsync(user, SystemRoles.SystemAdministrator);

        var company = await db.LegalEntities.SingleOrDefaultAsync(item => item.Code == "TEST-COMPANY", token);
        if (company is null)
        {
            company = new LegalEntity { Code = "TEST-COMPANY", Name = "测试工程有限公司", ShortName = "测试工程", CompanyCategoryId = CompanyCategoryDefaults.OtherId, LegalRepresentative = "测试法人", InvoiceTitle = "测试工程有限公司" };
            db.LegalEntities.Add(company);
        }
        var partner = await db.BusinessPartners.SingleOrDefaultAsync(item => item.PartnerNumber == "TEST-SUPPLIER", token);
        if (partner is null)
        {
            partner = new BusinessPartner { PartnerNumber = "TEST-SUPPLIER", Name = "测试设备出租方", ShortName = "测试出租方" };
            partner.Roles.Add(new BusinessPartnerRole { Partner = partner, RoleType = BusinessPartnerRoleType.MiscellaneousSupplier });
            db.BusinessPartners.Add(partner);
        }
        var project = await db.Projects.Include(item => item.LegalEntities).SingleOrDefaultAsync(item => item.ProjectNumber == "TEST-PROJECT", token);
        if (project is null)
        {
            project = new Project { ProjectNumber = "TEST-PROJECT", Name = "测试综合项目", Stage = ProjectStage.UnderConstruction };
            project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
            db.Projects.Add(project);
        }
        if (!await db.Employees.AnyAsync(item => item.EmployeeNumber == "TEST-EMPLOYEE", token)) db.Employees.Add(new Employee { EmployeeNumber = "TEST-EMPLOYEE", Name = "测试员工", EmployeeType = EmployeeType.Formal, DefaultLegalEntity = company, DefaultMonthlySalary = 8000m });
        if (!await db.FinancialAccounts.AnyAsync(item => item.LegalEntityId == company.Id && item.AccountName == "测试基本户", token)) db.FinancialAccounts.Add(new FinancialAccount { LegalEntity = company, AccountName = "测试基本户", AccountType = FinancialAccountType.Bank, OpeningBalance = 10000m, IsDefaultCollection = true, IsDefaultPayment = true, IsDefaultInvoice = true });
        if (!await db.Equipment.AnyAsync(item => item.EquipmentNumber == "TEST-EQUIPMENT", token)) db.Equipment.Add(new Data.Equipment { EquipmentNumber = "TEST-EQUIPMENT", Name = "测试挖掘机", Model = "TEST-200", Category = "挖掘机", OwnershipType = EquipmentOwnershipType.SelfOwned, OwnerLegalEntity = company, InternalDailyRate = 800m });
        if (!await db.Contracts.AnyAsync(item => item.ProjectId == project.Id && item.ContractNumber == "TEST-CONTRACT", token))
        {
            var contract = new Contract { Project = project, ContractNumber = "TEST-CONTRACT", Name = "测试施工合同", TotalAmount = 1000000m };
            contract.LegalEntityAllocations.Add(new ContractLegalEntityAllocation { Contract = contract, LegalEntity = company, Amount = 1000000m });
            contract.LineItems.Add(new ContractLineItem { Contract = contract, Code = "TEST-BOQ", Name = "测试工程量", Unit = "项", EstimatedQuantity = 1m, EstimatedUnitPrice = 1000000m });
            db.Contracts.Add(contract);
        }
        await db.SaveChangesAsync(token);
        var appData = Path.Combine(contentRootPath, "App_Data"); Directory.CreateDirectory(appData);
        var credentials = $"测试环境专用账号（禁止用于生产）{Environment.NewLine}用户名：{AdministratorUserName}{Environment.NewLine}密码：{password}{Environment.NewLine}数据库：{databaseName}{Environment.NewLine}";
        await File.WriteAllTextAsync(Path.Combine(appData, "local-test-credentials.txt"), credentials, new UTF8Encoding(false), token);
    }
}
