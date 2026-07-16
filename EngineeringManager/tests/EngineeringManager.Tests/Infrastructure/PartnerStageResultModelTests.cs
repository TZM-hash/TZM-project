using EngineeringManager.Domain.Partners;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.StageResults;
using EngineeringManager.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class PartnerStageResultModelTests
{
    [Fact]
    public async Task PartnerWithMultipleRolesAndStageResultAttachmentsCanBePersisted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var user = new ApplicationUser { UserName = "site-user", NormalizedUserName = "SITE-USER", DisplayName = "现场人员" };
        var project = new Project { ProjectNumber = "P-STAGE-01", Name = "阶段成果项目", Stage = ProjectStage.UnderConstruction };
        var contract = new Contract { Project = project, ContractNumber = "C-STAGE-01", Name = "施工合同", TotalAmount = 100m };
        var lineItem = new ContractLineItem { Contract = contract, Code = "001", Name = "主体工程", Unit = "m²", EstimatedQuantity = 100m, EstimatedUnitPrice = 1m };
        contract.LineItems.Add(lineItem);
        project.Contracts.Add(contract);
        var partner = new BusinessPartner { PartnerNumber = "BP-001", Name = "综合供应施工单位", ShortName = "综合单位" };
        partner.Roles.Add(new BusinessPartnerRole { Partner = partner, RoleType = BusinessPartnerRoleType.ConstructionCrew, TradeCategory = "土建" });
        partner.Roles.Add(new BusinessPartnerRole { Partner = partner, RoleType = BusinessPartnerRoleType.MaterialSupplier, TradeCategory = "辅材" });
        partner.Contacts.Add(new PartnerContact { Partner = partner, Name = "张联系人", Phone = "13800000000", IsPrimary = true });
        project.Partners.Add(new ProjectPartner { Project = project, Partner = partner, RoleType = BusinessPartnerRoleType.ConstructionCrew, Contract = contract });
        var stageResult = new StageResult
        {
            Project = project,
            Contract = contract,
            Title = "主体阶段完成",
            ResultType = StageResultType.Progress,
            Status = StageResultStatus.Recorded,
            ResultDate = new DateOnly(2026, 7, 16),
            QualityResult = QualityResult.Qualified,
            SubmittedByUser = user
        };
        stageResult.Lines.Add(new StageResultLine
        {
            StageResult = stageResult,
            ContractLineItem = lineItem,
            PeriodQuantity = 20m,
            CumulativeQuantity = 20m,
            RemainingQuantity = 80m,
            CompletionPercentage = 20m
        });
        stageResult.Attachments.Add(new Attachment
        {
            StageResult = stageResult,
            Project = project,
            StoredName = "safe-guid-photo.jpg",
            OriginalFileName = "现场照片.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 1024,
            Category = AttachmentCategory.Photo,
            UploadedByUser = user
        });

        db.AddRange(user, project, partner, stageResult);
        await db.SaveChangesAsync();

        (await db.BusinessPartnerRoles.CountAsync()).Should().Be(2);
        (await db.ProjectPartners.SingleAsync()).ContractId.Should().Be(contract.Id);
        (await db.StageResultLines.SingleAsync()).CumulativeQuantity.Should().Be(20m);
        (await db.Attachments.SingleAsync()).StoredName.Should().Be("safe-guid-photo.jpg");
    }
}
