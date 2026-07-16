using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class EmployeeModelTests
{
    [Fact]
    public async Task FormalAndLaborEmployeesWithAffiliationHistoryCanBePersisted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var department = new OrganizationUnit { Code = "EMP-DEPT", Name = "员工测试部门", UnitType = OrganizationUnitType.Department };
        var legalEntity = new LegalEntity { Code = "EMP-LE", Name = "员工测试公司", ShortName = "员工公司" };
        var crew = new BusinessPartner { PartnerNumber = "EMP-CREW", Name = "员工测试班组", ShortName = "测试班组" };
        crew.Roles.Add(new BusinessPartnerRole { Partner = crew, RoleType = BusinessPartnerRoleType.ConstructionCrew });
        var project = new Project { ProjectNumber = "EMP-P", Name = "员工测试项目", Stage = ProjectStage.UnderConstruction };
        var formal = new Employee { EmployeeNumber = "E-001", Name = "正式员工", EmployeeType = EmployeeType.Formal, DefaultLegalEntity = legalEntity };
        var labor = new Employee { EmployeeNumber = "L-001", Name = "劳务员工", EmployeeType = EmployeeType.Labor };
        formal.AffiliationHistory.Add(new EmployeeAffiliationHistory { Employee = formal, Department = department, Project = project, LegalEntity = legalEntity, StartDate = new DateOnly(2026, 1, 1), IsPrimary = true, PositionTitle = "项目经理" });
        labor.AffiliationHistory.Add(new EmployeeAffiliationHistory { Employee = labor, Project = project, CrewBusinessPartner = crew, LegalEntity = legalEntity, StartDate = new DateOnly(2026, 2, 1), IsPrimary = true, PositionTitle = "钢筋工" });

        db.AddRange(department, legalEntity, crew, project, formal, labor);
        await db.SaveChangesAsync();

        (await db.Employees.CountAsync()).Should().Be(2);
        (await db.EmployeeAffiliationHistories.SingleAsync(item => item.EmployeeId == labor.Id)).CrewBusinessPartnerId.Should().Be(crew.Id);
    }
}
