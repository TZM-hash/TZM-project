using System.Text;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Equipment;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.Reminders;
using EngineeringManager.Domain.Security;
using EngineeringManager.Domain.StageResults;
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

    public async Task<SampleDataContext> BuildCompleteAsync(string contentRootPath, CancellationToken token)
    {
        var context = await BuildCoreAsync(token);
        var accounts = await EnsureFinancialAccountsAsync(context, token);
        await EnsureFinanceAsync(context, accounts, token);
        await EnsurePayrollAndEmployeeLedgerAsync(context, accounts, token);
        await EnsureEquipmentAsync(context, token);
        await EnsureStageResultsAsync(context, contentRootPath, token);
        await EnsureRemindersAsync(context, token);
        await ValidateCompleteScenarioAsync(token);
        return context;
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

    private async Task<IReadOnlyDictionary<Guid, FinancialAccount>> EnsureFinancialAccountsAsync(SampleDataContext context, CancellationToken token)
    {
        var accounts = await db.FinancialAccounts.Where(item => item.AccountName.StartsWith("演示")).ToListAsync(token);
        for (var companyIndex = 0; companyIndex < context.Companies.Count; companyIndex++)
        {
            var company = context.Companies[companyIndex];
            if (accounts.Any(item => item.LegalEntityId == company.Id)) continue;
            var number = companyIndex + 1;
            var account = new FinancialAccount
            {
                LegalEntity = company,
                AccountName = $"演示基本户{number}",
                AccountNumber = $"622200000000{number:0000}",
                BankName = "演示商业银行昆明分行",
                AccountType = FinancialAccountType.Bank,
                OpeningBalance = 180_000m + number * 35_000m,
                IsDefaultCollection = true,
                IsDefaultPayment = true,
                IsDefaultInvoice = true
            };
            db.FinancialAccounts.Add(account);
            accounts.Add(account);
        }
        await db.SaveChangesAsync(token);
        return accounts.ToDictionary(item => item.LegalEntityId);
    }

    private async Task EnsureFinanceAsync(SampleDataContext context, IReadOnlyDictionary<Guid, FinancialAccount> accounts, CancellationToken token)
    {
        if (await db.ReceivableEntries.AnyAsync(item => item.Description != null && item.Description.StartsWith("DEMO-RCV-"), token)) return;
        for (var index = 0; index < 24; index++)
        {
            var project = context.Projects[index % context.Projects.Count];
            var contract = project.Contracts.OrderBy(item => item.ContractNumber).ElementAt(index % project.Contracts.Count);
            var allocation = contract.LegalEntityAllocations.Single();
            var company = allocation.LegalEntity;
            var partner = contract.BusinessPartner ?? context.Partners[index % context.Partners.Count];
            var account = accounts[company.Id];
            var entryDate = context.AnchorDate.AddMonths(-(23 - index) / 2).AddDays(index % 18);
            var receivableAmount = 280_000m + index * 21_500m;
            var payableAmount = decimal.Round(receivableAmount * (0.38m + index % 3 * 0.04m), 2);
            var collectionRatio = new[] { 0.45m, 0.65m, 0.80m, 1.00m }[index % 4];
            var paymentRatio = new[] { 0.50m, 0.70m, 0.85m }[index % 3];
            var invoiceRatio = new[] { 0.35m, 0.60m, 0.75m, 0.90m }[index % 4];
            var receivable = new ReceivableEntry
            {
                Project = project,
                Contract = contract,
                LegalEntity = company,
                BusinessPartner = partner,
                SourceType = index % 2 == 0 ? ReceivableSourceType.ContractMilestone : ReceivableSourceType.StageSettlement,
                EntryDate = entryDate,
                DueDate = entryDate.AddDays(45),
                Amount = receivableAmount,
                Description = $"DEMO-RCV-{index + 1:000} 阶段应收"
            };
            var collection = new CollectionEntry
            {
                Receivable = receivable,
                Project = project,
                Contract = contract,
                LegalEntity = company,
                BusinessPartner = partner,
                Account = account,
                CollectionDate = entryDate.AddDays(12 + index % 20),
                Amount = decimal.Round(receivableAmount * collectionRatio, 2),
                PaymentMethod = index % 6 == 0 ? PaymentMethod.Other : PaymentMethod.BankTransfer,
                Notes = $"DEMO-COL-{index + 1:000} 演示收款"
            };
            var payable = new PayableEntry
            {
                Project = project,
                Contract = contract,
                LegalEntity = company,
                BusinessPartner = context.Partners[(index + 1) % context.Partners.Count],
                SourceType = PayableSourceType.Settlement,
                EntryDate = entryDate.AddDays(3),
                DueDate = entryDate.AddDays(35),
                Amount = payableAmount,
                Description = $"DEMO-PAYABLE-{index + 1:000} 班组及材料应付"
            };
            var payment = new PaymentEntry
            {
                Payable = payable,
                Project = project,
                Contract = contract,
                LegalEntity = company,
                BusinessPartner = payable.BusinessPartner,
                Account = account,
                PaymentDate = entryDate.AddDays(18 + index % 12),
                Amount = decimal.Round(payableAmount * paymentRatio, 2),
                PaymentMethod = PaymentMethod.BankTransfer,
                Notes = $"DEMO-PMT-{index + 1:000} 演示付款"
            };
            var invoiceGross = decimal.Round(receivableAmount * invoiceRatio, 2);
            var invoiceTax = decimal.Round(invoiceGross / 1.09m * 0.09m, 2);
            var invoice = new InvoiceEntry
            {
                Project = project,
                Contract = contract,
                LegalEntity = company,
                BusinessPartner = partner,
                Direction = InvoiceDirection.Output,
                InvoiceNumber = $"DEMO-INV-{context.AnchorDate.Year}-{index + 1:000}",
                InvoiceDate = entryDate.AddDays(8),
                InvoiceType = "增值税发票",
                TaxRate = 0.09m,
                NetAmount = invoiceGross - invoiceTax,
                TaxAmount = invoiceTax,
                GrossAmount = invoiceGross,
                Status = InvoiceStatus.IssuedOrReceived
            };
            db.AddRange(receivable, collection, payable, payment, invoice);
            db.AccountTransactions.AddRange(
                new AccountTransaction { Account = account, Direction = AccountTransactionDirection.Inflow, SourceType = AccountTransactionSourceType.Collection, SourceId = collection.Id, TransactionDate = collection.CollectionDate, Amount = collection.Amount, Description = collection.Notes },
                new AccountTransaction { Account = account, Direction = AccountTransactionDirection.Outflow, SourceType = AccountTransactionSourceType.Payment, SourceId = payment.Id, TransactionDate = payment.PaymentDate, Amount = payment.Amount, Description = payment.Notes });
            if (index is 5 or 17)
            {
                db.RefundOrReversalEntries.Add(new RefundOrReversalEntry { Collection = collection, Receivable = receivable, Account = account, EntryDate = collection.CollectionDate.AddDays(5), Amount = 12_000m, AdjustmentType = FinancialAdjustmentType.Refund, Reason = "演示退款调整" });
            }
            if (index % 6 == 0)
            {
                db.DeductionEntries.Add(new DeductionEntry { Payable = payable, Project = project, LegalEntity = company, BusinessPartner = payable.BusinessPartner, EntryDate = payment.PaymentDate, Amount = decimal.Round(payableAmount * 0.03m, 2), Reason = "质量扣款（演示）" });
            }
        }
        await db.SaveChangesAsync(token);
    }

    private async Task EnsurePayrollAndEmployeeLedgerAsync(SampleDataContext context, IReadOnlyDictionary<Guid, FinancialAccount> accounts, CancellationToken token)
    {
        if (await db.PayrollBatches.AnyAsync(item => item.BatchNumber.StartsWith("DEMO-PB-"), token)) return;
        for (var monthOffset = 11; monthOffset >= 0; monthOffset--)
        {
            var month = context.AnchorDate.AddMonths(-monthOffset);
            var start = new DateOnly(month.Year, month.Month, 1);
            var end = start.AddMonths(1).AddDays(-1);
            var company = context.Companies[(11 - monthOffset) % context.Companies.Count];
            var account = accounts[company.Id];
            var batch = new PayrollBatch
            {
                BatchNumber = $"DEMO-PB-{start:yyyy-MM}",
                Name = $"{start:yyyy年MM月}演示工资",
                BatchType = PayrollBatchType.Monthly,
                StartDate = start,
                EndDate = end,
                LegalEntity = company,
                Project = context.Projects[(11 - monthOffset) % context.Projects.Count],
                Status = monthOffset == 0 ? PayrollBatchStatus.Confirmed : PayrollBatchStatus.Closed,
                Notes = "演示工资批次"
            };
            for (var employeeOffset = 0; employeeOffset < 10; employeeOffset++)
            {
                var employee = context.Employees[((11 - monthOffset) * 2 + employeeOffset) % context.Employees.Count];
                var earning = employee.EmployeeType == EmployeeType.Formal
                    ? employee.DefaultMonthlySalary ?? 7000m
                    : (employee.DefaultDailyRate ?? 300m) * (18 + employeeOffset % 6);
                var item = new PayrollItem { Batch = batch, Employee = employee, ItemType = employee.EmployeeType == EmployeeType.Formal ? PayrollItemType.FixedSalary : PayrollItemType.DailyWage, Nature = PayrollItemNature.Earning, Quantity = employee.EmployeeType == EmployeeType.Labor ? 18 + employeeOffset % 6 : null, UnitPrice = employee.EmployeeType == EmployeeType.Labor ? employee.DefaultDailyRate : null, Amount = earning, Description = "演示应发工资" };
                item.CostAllocations.Add(new PayrollCostAllocation { PayrollItem = item, Project = batch.Project!, LegalEntity = company, Amount = earning });
                batch.Items.Add(item);
                if (employeeOffset % 4 == 0) batch.Items.Add(new PayrollItem { Batch = batch, Employee = employee, ItemType = PayrollItemType.AdvanceDeduction, Nature = PayrollItemNature.Deduction, Amount = 500m, Description = "演示借支抵扣" });
                var paymentAmount = monthOffset == 0 && employeeOffset >= 7 ? decimal.Round(earning * 0.55m, 2) : Math.Max(earning - (employeeOffset % 4 == 0 ? 500m : 0m), 0m);
                batch.Payments.Add(new PayrollPayment { Batch = batch, Employee = employee, Account = account, PaymentDate = end.AddDays(5), Amount = paymentAmount, PaymentMethod = employeeOffset % 5 == 0 ? PaymentMethod.WeChat : PaymentMethod.BankTransfer, PayeeType = PayrollPayeeType.Employee, PayeeName = employee.Name, Notes = "演示工资发放" });
            }
            db.PayrollBatches.Add(batch);
        }

        for (var index = 0; index < 8; index++)
        {
            var employee = context.Employees[index];
            var company = context.Companies[index % context.Companies.Count];
            var account = accounts[company.Id];
            var project = context.Projects[index % context.Projects.Count];
            var expense = new ExpenseRecord { Employee = employee, Project = project, LegalEntity = company, ExpenseDate = context.AnchorDate.AddDays(-(index * 11 + 3)), Category = index % 2 == 0 ? "差旅费" : "现场材料垫付", Amount = 800m + index * 260m, Description = "演示员工报销" };
            if (index % 3 != 0) expense.Payments.Add(new ExpensePayment { Expense = expense, Account = account, PaymentDate = expense.ExpenseDate.AddDays(7), Amount = expense.Amount, PaymentMethod = PaymentMethod.BankTransfer, RecordKind = EmployeeLedgerRecordKind.Payment, Notes = "演示报销付款" });
            db.ExpenseRecords.Add(expense);
            db.EmployeeAdvances.Add(new EmployeeAdvance { Employee = employee, Project = project, LegalEntity = company, Account = account, EntryDate = context.AnchorDate.AddDays(-(index * 9 + 5)), Amount = 1_500m + index * 300m, Action = EmployeeAdvanceAction.Disbursement, Description = "演示员工借支" });
            var type = index % 2 == 0 ? EmployeeLedgerEntryType.Dividend : EmployeeLedgerEntryType.Interest;
            db.EmployeeOtherPayments.Add(new EmployeeOtherPayment { Employee = employee, Project = project, LegalEntity = company, EntryType = type, RecordKind = EmployeeLedgerRecordKind.Payable, EntryDate = context.AnchorDate.AddDays(-(index * 15 + 2)), Amount = 2_000m + index * 650m, Description = type == EmployeeLedgerEntryType.Dividend ? "演示分红应付" : "演示利息应付" });
        }
        await db.SaveChangesAsync(token);
    }

    private async Task EnsureEquipmentAsync(SampleDataContext context, CancellationToken token)
    {
        if (await db.Equipment.AnyAsync(item => item.EquipmentNumber.StartsWith("DEMO-EQ-"), token)) return;
        for (var index = 0; index < SampleDataCatalog.EquipmentCount; index++)
        {
            var number = index + 1;
            var rented = index >= 9;
            var company = context.Companies[index % context.Companies.Count];
            var lessor = context.Partners[(index * 2 + 3) % context.Partners.Count];
            var entryDate = context.AnchorDate.AddDays(-(100 + index * 4));
            var exitDate = index % 4 == 0 ? context.AnchorDate.AddDays(-(8 + index)) : context.AnchorDate.AddDays(30 - index);
            var equipment = new Data.Equipment
            {
                EquipmentNumber = $"DEMO-EQ-{number:000}",
                Name = new[] { "旋挖钻机", "履带吊", "挖掘机", "装载机", "泥浆泵" }[index % 5] + number,
                Model = new[] { "SR285", "QUY80", "SY365", "LW500", "BW-250" }[index % 5],
                Category = new[] { "桩工机械", "起重设备", "土方设备", "装载设备", "辅助设备" }[index % 5],
                OwnershipType = rented ? EquipmentOwnershipType.Rented : EquipmentOwnershipType.SelfOwned,
                Status = exitDate <= context.AnchorDate ? EquipmentStatus.Idle : EquipmentStatus.InUse,
                OwnerLegalEntity = rented ? null : company,
                LessorBusinessPartner = rented ? lessor : null,
                PurchaseDate = rented ? null : context.AnchorDate.AddYears(-(2 + index % 6)),
                PurchaseAmount = rented ? null : 680_000m + index * 75_000m,
                InternalDailyRate = rented ? null : 1_200m + index * 80m,
                Notes = "演示设备"
            };
            EquipmentLeaseAgreement? lease = null;
            var rentMode = rented && index % 2 == 0 ? RentMode.Monthly : RentMode.Daily;
            var unitRate = rented ? rentMode == RentMode.Monthly ? 48_000m + index * 1_500m : 2_200m + index * 120m : equipment.InternalDailyRate!.Value;
            if (rented)
            {
                lease = new EquipmentLeaseAgreement { Equipment = equipment, LessorBusinessPartner = lessor, ContractNumber = $"DEMO-LEASE-{number:000}", StartDate = entryDate.AddDays(-10), EndDate = exitDate.AddDays(15), RentMode = rentMode, MonthlyProrationMode = MonthlyProrationMode.ThirtyDay, UnitRate = unitRate, Notes = "演示租赁协议" };
                equipment.LeaseAgreements.Add(lease);
            }
            var project = context.Projects[index % context.Projects.Count];
            var usage = new EquipmentProjectUsage { Equipment = equipment, Project = project, LegalEntity = company, LeaseAgreement = lease, EntryDate = entryDate, ExitDate = exitDate, RentMode = rentMode, MonthlyProrationMode = MonthlyProrationMode.ThirtyDay, UnitRate = unitRate, Notes = "演示项目设备使用" };
            var workEnd = entryDate.AddDays(44);
            var stopStart = workEnd.AddDays(1);
            var stopEnd = stopStart.AddDays(7);
            var finalWorkStart = stopEnd.AddDays(1);
            usage.Periods.Add(new EquipmentWorkPeriod { Usage = usage, StartDate = entryDate, EndDate = workEnd, PeriodType = EquipmentPeriodType.Work, IsChargeable = true, Notes = "首段施工" });
            usage.Periods.Add(new EquipmentWorkPeriod { Usage = usage, StartDate = stopStart, EndDate = stopEnd, PeriodType = EquipmentPeriodType.Stop, IsChargeable = index % 3 == 0, Notes = "现场停工" });
            usage.Periods.Add(new EquipmentWorkPeriod { Usage = usage, StartDate = finalWorkStart, EndDate = exitDate, PeriodType = EquipmentPeriodType.Work, IsChargeable = true, Notes = "恢复施工" });
            var inputs = usage.Periods.Select(item => new EquipmentUsagePeriodInput(item.StartDate, item.EndDate, item.PeriodType, item.IsChargeable)).ToArray();
            var calculation = EquipmentRentCalculator.Calculate(new EquipmentRentInput(rentMode, unitRate, MonthlyProrationMode.ThirtyDay, entryDate, exitDate, inputs, []));
            if (index < 10)
            {
                usage.Settlement = new EquipmentSettlement { Usage = usage, SettlementDate = exitDate, BaseAmount = calculation.BaseAmount, TotalAmount = calculation.TotalAmount, ModificationReason = "演示首次结算" };
            }
            equipment.ProjectUsages.Add(usage);
            equipment.MaintenanceRecords.Add(new EquipmentMaintenanceRecord { Equipment = equipment, MaintenanceType = "定期保养", MaintenanceDate = context.AnchorDate.AddDays(-(60 + index)), NextDueDate = context.AnchorDate.AddDays(10 + index * 3), Amount = 1_200m + index * 150m, Provider = "演示维保服务商", Notes = "演示维保记录" });
            db.Equipment.Add(equipment);
        }
        await db.SaveChangesAsync(token);
    }

    private async Task EnsureStageResultsAsync(SampleDataContext context, string contentRootPath, CancellationToken token)
    {
        if (await db.StageResults.AnyAsync(item => item.Title.StartsWith("DEMO-RESULT-"), token)) return;
        var attachmentDirectory = Path.Combine(contentRootPath, "App_Data", "attachments");
        Directory.CreateDirectory(attachmentDirectory);
        for (var index = 0; index < 12; index++)
        {
            var project = context.Projects[index];
            var contract = project.Contracts.OrderBy(item => item.ContractNumber).First();
            var line = contract.LineItems.OrderBy(item => item.SortOrder).First();
            var estimated = line.EstimatedQuantity ?? 0m;
            var cumulative = decimal.Round(estimated * (0.35m + index % 6 * 0.1m), 2);
            var storedName = $"{Guid.NewGuid():N}.txt";
            var bytes = Encoding.UTF8.GetBytes($"DEMO 测试附件：{project.ProjectNumber} 阶段成果 {index + 1}");
            await File.WriteAllBytesAsync(Path.Combine(attachmentDirectory, storedName), bytes, token);
            var result = new StageResult
            {
                Project = project,
                Contract = contract,
                Title = $"DEMO-RESULT-{index + 1:000} {project.Name}阶段成果",
                ResultType = index % 4 == 3 ? StageResultType.SettlementSupport : StageResultType.Progress,
                Status = StageResultStatus.Recorded,
                ResultDate = context.AnchorDate.AddDays(-(index * 17 + 4)),
                Description = "演示阶段成果和工程量汇总",
                QualityResult = index % 5 == 0 ? QualityResult.ConditionallyQualified : QualityResult.Qualified,
                SubmittedByUser = context.Users.Single(item => item.UserName == "demo-site-staff"),
                SubmittedAt = DateTimeOffset.UtcNow.AddDays(-index)
            };
            result.Lines.Add(new StageResultLine { StageResult = result, ContractLineItem = line, PeriodQuantity = decimal.Round(estimated * 0.1m, 2), CumulativeQuantity = cumulative, RemainingQuantity = Math.Max(estimated - cumulative, 0m), CompletionPercentage = estimated == 0m ? 0m : decimal.Round(cumulative / estimated * 100m, 1), ExceedsTarget = cumulative > estimated, Notes = "演示工程量" });
            result.Attachments.Add(new Attachment { StageResult = result, Project = project, Contract = contract, StoredName = storedName, OriginalFileName = $"{project.ProjectNumber}-阶段成果说明.txt", ContentType = "text/plain", SizeBytes = bytes.Length, Category = AttachmentCategory.Quantity, Description = "测试资料占位文件", UploadedByUser = result.SubmittedByUser });
            db.StageResults.Add(result);
        }
        await db.SaveChangesAsync(token);
    }

    private async Task EnsureRemindersAsync(SampleDataContext context, CancellationToken token)
    {
        if (await db.ReminderItems.AnyAsync(item => item.DeduplicationKey.StartsWith("demo-"), token)) return;
        (ReminderType Type, ReminderSeverity Severity, string Title, string Message, int DueOffset, decimal? Amount)[] definitions =
        {
            (ReminderType.UncollectedReceivable, ReminderSeverity.Critical, "重点项目未收款", "存在超过计划收款日的项目应收。", -8, 480_000m),
            (ReminderType.UninvoicedReceivable, ReminderSeverity.Warning, "项目待开票", "部分已确认应收尚未开票。", 5, 260_000m),
            (ReminderType.UnpaidPayable, ReminderSeverity.Warning, "班组款待支付", "存在已确认但尚未支付的班组款。", 3, 180_000m),
            (ReminderType.UnpaidPayroll, ReminderSeverity.Critical, "本期工资未发完", "当前工资批次仍有未发金额。", 2, 32_000m),
            (ReminderType.EquipmentLeaseExpiring, ReminderSeverity.Warning, "租赁设备即将到期", "旋挖钻机租赁协议将在十五天内到期。", 12, null),
            (ReminderType.EquipmentMaintenanceDue, ReminderSeverity.Info, "设备维保即将到期", "三台设备将在三十天内到达维保日期。", 20, null),
            (ReminderType.ProjectMilestone, ReminderSeverity.Info, "项目节点临近", "阶段验收节点即将到期。", 7, null),
            (ReminderType.OfflineSyncFailed, ReminderSeverity.Warning, "现场草稿同步异常", "一份演示现场草稿等待重新同步。", 0, null)
        };
        for (var index = 0; index < definitions.Length; index++)
        {
            var item = definitions[index];
            var project = context.Projects[index % context.Projects.Count];
            db.ReminderItems.Add(new ReminderItem { DeduplicationKey = $"demo-reminder-{index + 1:00}", Type = item.Type, Severity = item.Severity, Title = item.Title, Message = item.Message, SourceType = "Project", SourceId = project.Id.ToString(), DueDate = context.AnchorDate.AddDays(item.DueOffset), Amount = item.Amount, FirstOccurredAt = DateTimeOffset.UtcNow.AddDays(-(index + 2)), LastOccurredAt = DateTimeOffset.UtcNow.AddDays(-index) });
        }
        await db.SaveChangesAsync(token);
    }

    private async Task ValidateCompleteScenarioAsync(CancellationToken token)
    {
        SampleDataAssertions.Require(await db.Equipment.CountAsync(item => item.EquipmentNumber.StartsWith("DEMO-EQ-"), token) == SampleDataCatalog.EquipmentCount, "设备数量不是 15 台");
        SampleDataAssertions.Require(await db.PayrollBatches.CountAsync(item => item.BatchNumber.StartsWith("DEMO-PB-"), token) == 12, "工资批次不是 12 个月");
        var receivable = await db.ReceivableEntries.Where(item => item.Description != null && item.Description.StartsWith("DEMO-RCV-")).SumAsync(item => item.Amount, token);
        var collected = await db.CollectionEntries.Where(item => item.Notes != null && item.Notes.StartsWith("DEMO-COL-")).SumAsync(item => item.Amount, token);
        var invoiced = await db.InvoiceEntries.Where(item => item.InvoiceNumber.StartsWith("DEMO-INV-")).SumAsync(item => item.GrossAmount, token);
        SampleDataAssertions.Require(receivable >= collected, "已收款大于应收款");
        SampleDataAssertions.Require(receivable >= invoiced, "已开票大于应开票");
    }

    private static void EnsureIdentitySuccess(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("；", result.Errors.Select(item => item.Description)));
        }
    }
}
