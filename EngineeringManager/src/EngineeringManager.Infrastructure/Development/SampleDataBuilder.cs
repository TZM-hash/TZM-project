using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Development;

public sealed record SampleCredential(string UserName, string Password, string DisplayName, string Role);

public sealed record SampleDataContext(
    DateOnly AnchorDate,
    IReadOnlyList<LegalEntity> Companies,
    IReadOnlyList<BusinessPartner> Partners,
    IReadOnlyList<Project> Projects,
    IReadOnlyList<Employee> Employees,
    IReadOnlyList<ApplicationUser> Users,
    IReadOnlyList<SampleCredential> Credentials);

public sealed class SampleDataBuilder(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider)
{
    private static readonly (string UserName, string DisplayName, string Role)[] UserDefinitions =
    [
        ("demo-system-admin", "演示系统管理员", SystemRoles.SystemAdministrator),
        ("demo-app-admin", "演示应用管理员", SystemRoles.ApplicationAdministrator),
        ("demo-finance", "演示财务人员", SystemRoles.Finance),
        ("demo-project-manager", "演示项目负责人", SystemRoles.ProjectManager),
        ("demo-site-staff", "演示现场人员", SystemRoles.SiteStaff)
    ];

    private static readonly ProjectStage[] ProjectStages =
    [
        ProjectStage.Preliminary,
        ProjectStage.AwaitingContract,
        ProjectStage.AwaitingMobilization,
        ProjectStage.UnderConstruction,
        ProjectStage.UnderConstruction,
        ProjectStage.Suspended,
        ProjectStage.CompletedAwaitingAcceptance,
        ProjectStage.Settlement,
        ProjectStage.Warranty,
        ProjectStage.Closed
    ];

    public async Task<SampleDataContext> BuildCoreAsync(CancellationToken token)
    {
        var anchor = SampleDataCatalog.AnchorDate(timeProvider);
        var (users, credentials) = await EnsureUsersAsync(token);
        var companies = await EnsureCompaniesAsync(token);
        var partners = await EnsurePartnersAsync(token);
        var employees = await EnsureEmployeesAsync(anchor, companies, token);
        var projects = await EnsureProjectsAsync(anchor, companies, partners, users, token);

        return new SampleDataContext(anchor, companies, partners, projects, employees, users, credentials);
    }

    private async Task<(IReadOnlyList<ApplicationUser> Users, IReadOnlyList<SampleCredential> Credentials)> EnsureUsersAsync(CancellationToken token)
    {
        var users = new List<ApplicationUser>();
        var credentials = new List<SampleCredential>();
        foreach (var definition in UserDefinitions)
        {
            token.ThrowIfCancellationRequested();
            var password = DevelopmentSampleDataSeeder.GenerateTestPassword();
            var user = await userManager.FindByNameAsync(definition.UserName);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = definition.UserName,
                    Email = $"{definition.UserName}@local.invalid",
                    DisplayName = definition.DisplayName,
                    EmailConfirmed = true
                };
                EnsureIdentitySuccess(await userManager.CreateAsync(user, password));
            }
            else
            {
                user.DisplayName = definition.DisplayName;
                user.IsEnabled = true;
                EnsureIdentitySuccess(await userManager.UpdateAsync(user));
                var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
                EnsureIdentitySuccess(await userManager.ResetPasswordAsync(user, resetToken, password));
            }

            if (!await userManager.IsInRoleAsync(user, definition.Role))
            {
                EnsureIdentitySuccess(await userManager.AddToRoleAsync(user, definition.Role));
            }

            users.Add(user);
            credentials.Add(new SampleCredential(definition.UserName, password, definition.DisplayName, definition.Role));
        }

        return (users, credentials);
    }

    private async Task<IReadOnlyList<LegalEntity>> EnsureCompaniesAsync(CancellationToken token)
    {
        var categories = new[]
        {
            CompanyCategoryDefaults.GeneralTaxpayerCompanyId,
            CompanyCategoryDefaults.SmallScaleCompanyId,
            CompanyCategoryDefaults.SmallScaleSoleProprietorId,
            CompanyCategoryDefaults.GeneralTaxpayerCompanyId,
            CompanyCategoryDefaults.SmallScaleCompanyId
        };
        var names = new[] { "云岭建设工程有限公司", "中福基础工程有限公司", "启程机械工程有限公司", "盛达市政工程有限公司", "安泰劳务服务中心" };
        var companies = await db.LegalEntities.Where(item => item.Code.StartsWith("DEMO-COMP-")).OrderBy(item => item.Code).ToListAsync(token);
        for (var index = companies.Count; index < SampleDataCatalog.CompanyCount; index++)
        {
            var number = index + 1;
            var company = new LegalEntity
            {
                Code = $"DEMO-COMP-{number:00}",
                Name = names[index],
                ShortName = names[index].Replace("有限公司", string.Empty).Replace("中心", string.Empty),
                CompanyCategoryId = categories[index],
                LegalRepresentative = $"测试法人{number}",
                UnifiedSocialCreditCode = $"91530000DEMO{number:000000}",
                RegisteredAddress = $"云南省昆明市测试大道{number}号",
                BusinessAddress = $"云南省昆明市工程路{number * 10}号",
                Phone = $"0871-6000{number:0000}",
                InvoiceTitle = names[index],
                Notes = "演示数据，仅用于 EngineeringManager_Test。"
            };
            db.LegalEntities.Add(company);
            companies.Add(company);
        }

        await db.SaveChangesAsync(token);
        return companies.OrderBy(item => item.Code).ToArray();
    }

    private async Task<IReadOnlyList<BusinessPartner>> EnsurePartnersAsync(CancellationToken token)
    {
        var partners = await db.BusinessPartners.Where(item => item.PartnerNumber.StartsWith("DEMO-BP-")).OrderBy(item => item.PartnerNumber).ToListAsync(token);
        for (var index = partners.Count; index < SampleDataCatalog.PartnerCount; index++)
        {
            var number = index + 1;
            var role = (index % 4) switch
            {
                0 => BusinessPartnerRoleType.CustomerOrGeneralContractor,
                1 => BusinessPartnerRoleType.ConstructionCrew,
                2 => BusinessPartnerRoleType.MaterialSupplier,
                _ => BusinessPartnerRoleType.MiscellaneousSupplier
            };
            var partner = new BusinessPartner
            {
                PartnerNumber = $"DEMO-BP-{number:00}",
                Name = role switch
                {
                    BusinessPartnerRoleType.CustomerOrGeneralContractor => $"示范总包单位{number}有限公司",
                    BusinessPartnerRoleType.ConstructionCrew => $"示范施工班组{number}",
                    BusinessPartnerRoleType.MaterialSupplier => $"示范材料供应商{number}有限公司",
                    _ => $"示范设备出租方{number}有限公司"
                },
                ShortName = $"演示合作方{number}",
                UnifiedSocialCreditCode = $"91530000PARTNER{number:0000}",
                Notes = "演示合作单位"
            };
            partner.Roles.Add(new BusinessPartnerRole
            {
                Partner = partner,
                RoleType = role,
                TradeCategory = role == BusinessPartnerRoleType.ConstructionCrew ? "桩基施工" : null,
                PricingRule = role == BusinessPartnerRoleType.ConstructionCrew ? "按工程量计价" : null,
                SettlementTerms = "阶段结算"
            });
            db.BusinessPartners.Add(partner);
            partners.Add(partner);
        }

        await db.SaveChangesAsync(token);
        return partners.OrderBy(item => item.PartnerNumber).ToArray();
    }

    private async Task<IReadOnlyList<Employee>> EnsureEmployeesAsync(DateOnly anchor, IReadOnlyList<LegalEntity> companies, CancellationToken token)
    {
        var employees = await db.Employees.Where(item => item.EmployeeNumber.StartsWith("DEMO-E-")).OrderBy(item => item.EmployeeNumber).ToListAsync(token);
        for (var index = employees.Count; index < SampleDataCatalog.EmployeeCount; index++)
        {
            var number = index + 1;
            var formal = index < 20;
            var employee = new Employee
            {
                EmployeeNumber = $"DEMO-E-{number:000}",
                Name = $"演示员工{number:00}",
                EmployeeType = formal ? EmployeeType.Formal : EmployeeType.Labor,
                Phone = $"1380000{number:0000}",
                HireDate = anchor.AddMonths(-(number % 36 + 3)),
                PositionTitle = formal ? new[] { "项目经理", "施工员", "资料员", "财务专员", "设备管理员" }[index % 5] : new[] { "桩机操作工", "焊工", "钢筋工", "普工" }[index % 4],
                DefaultLegalEntity = companies[index % companies.Count],
                DefaultMonthlySalary = formal ? 6500m + index * 180m : null,
                DefaultDailyRate = formal ? null : 260m + index * 8m,
                IsActive = number != 30
            };
            db.Employees.Add(employee);
            employees.Add(employee);
        }

        await db.SaveChangesAsync(token);
        return employees.OrderBy(item => item.EmployeeNumber).ToArray();
    }

    private async Task<IReadOnlyList<Project>> EnsureProjectsAsync(
        DateOnly anchor,
        IReadOnlyList<LegalEntity> companies,
        IReadOnlyList<BusinessPartner> partners,
        IReadOnlyList<ApplicationUser> users,
        CancellationToken token)
    {
        var projects = await db.Projects
            .Include(item => item.Contracts).ThenInclude(item => item.LineItems)
            .Include(item => item.Contracts).ThenInclude(item => item.LegalEntityAllocations)
            .Include(item => item.LegalEntities)
            .Where(item => item.ProjectNumber.StartsWith("DEMO-P-"))
            .OrderBy(item => item.ProjectNumber)
            .ToListAsync(token);
        var manager = users.Single(item => item.UserName == "demo-project-manager");
        var siteStaff = users.Single(item => item.UserName == "demo-site-staff");

        for (var index = projects.Count; index < SampleDataCatalog.ProjectCount; index++)
        {
            var number = index + 1;
            var stage = ProjectStages[index % ProjectStages.Length];
            var project = new Project
            {
                ProjectNumber = $"DEMO-P-{number:000}",
                Name = new[] { "城市快速路桩基工程", "物流园地基处理工程", "产业园基础施工项目", "高速公路桥梁桩基项目", "医院综合楼基础工程" }[index % 5] + $"（演示{number}）",
                GeneralContractorName = partners[index % partners.Count].Name,
                GeneralContractorContact = $"总包联系人{number}",
                GeneralContractorPhone = $"1390000{number:0000}",
                ResponsibleUserId = manager.Id,
                Stage = stage,
                CreatedAt = anchor.AddMonths(-(index % 12 + 1)).ToDateTime(TimeOnly.MinValue),
                UpdatedAt = anchor.AddDays(-(index % 35)).ToDateTime(TimeOnly.MinValue)
            };
            var companyCount = number == 4 ? 2 : number == 9 ? 3 : 1;
            var projectCompanies = Enumerable.Range(0, companyCount).Select(offset => companies[(index + offset) % companies.Count]).ToArray();
            for (var companyIndex = 0; companyIndex < projectCompanies.Length; companyIndex++)
            {
                project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = projectCompanies[companyIndex], IsPrimary = companyIndex == 0 });
                project.Contracts.Add(CreateContract(anchor, project, partners[index % partners.Count], projectCompanies[companyIndex], number, companyIndex, stage));
            }
            project.Assignments.Add(new ProjectAssignment { Project = project, User = manager, UserId = manager.Id, AssignmentType = ProjectAssignmentType.Responsible });
            project.Assignments.Add(new ProjectAssignment { Project = project, User = siteStaff, UserId = siteStaff.Id, AssignmentType = ProjectAssignmentType.SiteStaff });
            project.Milestones.Add(new ProjectMilestone { Project = project, Name = "计划进场", PlannedDate = anchor.AddMonths(-6 + index % 4), ActualDate = stage >= ProjectStage.UnderConstruction ? anchor.AddMonths(-5 + index % 4) : null, IsCompleted = stage >= ProjectStage.UnderConstruction, SortOrder = 10 });
            project.Milestones.Add(new ProjectMilestone { Project = project, Name = "计划完工", PlannedDate = anchor.AddMonths(2 + index % 5), ActualDate = stage >= ProjectStage.Settlement ? anchor.AddMonths(-1) : null, IsCompleted = stage >= ProjectStage.Settlement, SortOrder = 20 });
            db.Projects.Add(project);
            projects.Add(project);
        }

        await db.SaveChangesAsync(token);
        return projects.OrderBy(item => item.ProjectNumber).ToArray();
    }

    private static Contract CreateContract(
        DateOnly anchor,
        Project project,
        BusinessPartner partner,
        LegalEntity company,
        int projectNumber,
        int companyIndex,
        ProjectStage stage)
    {
        var total = 1_200_000m + projectNumber * 135_000m + companyIndex * 280_000m;
        var contract = new Contract
        {
            Project = project,
            BusinessPartner = partner,
            ContractNumber = $"DEMO-C-{projectNumber:000}-{companyIndex + 1:00}",
            Name = companyIndex == 0 ? "桩基工程施工主合同" : $"分公司施工合同{companyIndex + 1}",
            ContractType = ContractType.MainContract,
            AllocationMode = ContractAllocationMode.SingleCompany,
            CounterpartyName = partner.Name,
            SignedDate = anchor.AddMonths(-(projectNumber % 10 + 2)),
            TotalAmount = total
        };
        contract.LegalEntityAllocations.Add(new ContractLegalEntityAllocation { Contract = contract, LegalEntity = company, Amount = total });
        var settled = stage >= ProjectStage.Settlement;
        var lineAmounts = new[] { total * 0.45m, total * 0.35m, total * 0.20m };
        var lineNames = new[] { "旋挖灌注桩施工", "钢筋笼制作安装", "设备进退场及措施费" };
        var units = new[] { "米", "吨", "项" };
        for (var lineIndex = 0; lineIndex < lineAmounts.Length; lineIndex++)
        {
            var quantity = lineIndex == 2 ? 1m : 100m + projectNumber * 5m + lineIndex * 20m;
            var price = decimal.Round(lineAmounts[lineIndex] / quantity, 2);
            contract.LineItems.Add(new ContractLineItem
            {
                Contract = contract,
                Code = $"BOQ-{lineIndex + 1:00}",
                Name = lineNames[lineIndex],
                Unit = units[lineIndex],
                EstimatedQuantity = quantity,
                EstimatedUnitPrice = price,
                SettledQuantity = settled ? decimal.Round(quantity * (0.96m + projectNumber % 4 * 0.01m), 2) : null,
                SettledUnitPrice = settled ? price : null,
                IsSettlementConfirmed = settled,
                SortOrder = (lineIndex + 1) * 10
            });
        }
        return contract;
    }

    private static void EnsureIdentitySuccess(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("；", result.Errors.Select(item => item.Description)));
        }
    }
}
