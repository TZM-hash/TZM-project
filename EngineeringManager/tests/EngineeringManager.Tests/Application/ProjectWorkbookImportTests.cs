using EngineeringManager.Application.DataExchange;
using EngineeringManager.Application.Finance;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.DataExchange;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.Security;
using EngineeringManager.Domain.StageResults;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.DataExchange;
using EngineeringManager.Infrastructure.Finance;
using EngineeringManager.Infrastructure.Files;
using EngineeringManager.Infrastructure.Projects;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Text;

namespace EngineeringManager.Tests.Application;

public sealed class ProjectWorkbookImportTests
{
    [Fact]
    public async Task PreviewAppliesPersistedPerSheetPermissionDenial()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        fixture.Db.Users.Add(new ApplicationUser { Id = "restricted-admin", UserName = "restricted-admin", DisplayName = "受限管理员" });
        fixture.Db.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            UserId = "restricted-admin",
            PermissionKey = PermissionKeys.ProjectsManage,
            Effect = PermissionEffect.Deny
        });
        await fixture.Db.SaveChangesAsync();
        var workbook = CreateWorkbook((ProjectWorkbookSheet.ProjectMaster, [Row(ProjectWorkbookSheet.ProjectMaster, new Dictionary<string, object?>
        {
            ["project_number"] = "DENIED-P",
            ["project_name"] = "不应允许导入",
            ["stage"] = "UnderConstruction",
            ["contract_signing_status"] = "NotSigned",
            ["affiliation_type"] = "SelfOperated",
            ["is_active"] = true
        })]));

        var preview = await fixture.Service.PreviewAsync(new ProjectWorkbookImportRequest(
            "restricted-admin",
            "受限项目.xlsx",
            workbook,
            Actor: new ProjectWorkbookActor("restricted-admin", [SystemRoles.ApplicationAdministrator])), CancellationToken.None);

        preview.Errors.Should().ContainSingle(item => item.ColumnName == "项目主档/权限");
        preview.ErrorRows.Should().Be(1);
        preview.ValidRows.Should().Be(0);
    }

    [Fact]
    public async Task CompleteWorkbookCreatesProjectContractAndQuantityInDependencyOrder()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var workbook = CreateWorkbook(
            (ProjectWorkbookSheet.ProjectMaster, [Row(ProjectWorkbookSheet.ProjectMaster, new Dictionary<string, object?>
            {
                ["project_number"] = "IMP-P", ["project_name"] = "导入项目", ["stage"] = "UnderConstruction",
                ["contract_signing_status"] = "NotSigned", ["affiliation_type"] = "SelfOperated", ["is_active"] = true
            })]),
            (ProjectWorkbookSheet.Contracts, [Row(ProjectWorkbookSheet.Contracts, new Dictionary<string, object?>
            {
                ["project_number"] = "IMP-P", ["contract_number"] = "IMP-C", ["name"] = "导入合同",
                ["contract_type"] = "MainContract", ["allocation_mode"] = "SingleCompany", ["total_amount"] = 100m, ["is_active"] = true
            })]),
            (ProjectWorkbookSheet.QuantityLines, [Row(ProjectWorkbookSheet.QuantityLines, new Dictionary<string, object?>
            {
                ["project_number"] = "IMP-P", ["contract_number"] = "IMP-C", ["code"] = "001", ["name"] = "导入工程量",
                ["unit"] = "项", ["quantity"] = 2m, ["unit_price"] = 5m, ["accounting_label"] = "暂估", ["requires_invoice"] = true
            })]));

        var preview = await fixture.Service.PreviewAsync(new ProjectWorkbookImportRequest("admin", "项目.xlsx", workbook, ImportMode.Mixed, Actor: ProjectWorkbookActor.Administrator("admin")), CancellationToken.None);
        await fixture.Service.ConfirmAsync(ProjectWorkbookActor.Administrator("admin"), preview.BatchId, CancellationToken.None);

        preview.Errors.Should().BeEmpty();
        (await fixture.Db.Projects.SingleAsync()).ProjectNumber.Should().Be("IMP-P");
        (await fixture.Db.Contracts.SingleAsync()).ContractNumber.Should().Be("IMP-C");
        (await fixture.Db.ContractLineItems.SingleAsync()).Quantity.Should().Be(2m);
    }

    [Fact]
    public async Task StandardWorkbookUpdatesBySystemIdAndClearsPresentBlankNullableField()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var project = new Project { ProjectNumber = "UPD-P", Name = "旧名称", Stage = ProjectStage.UnderConstruction, Notes = "旧备注" };
        fixture.Db.Projects.Add(project);
        await fixture.Db.SaveChangesAsync();
        var originalStamp = project.ConcurrencyStamp;
        var definition = ProjectWorkbookCatalog.Get(ProjectWorkbookSheet.ProjectMaster);
        var values = definition.Fields.Select(field => field.Key switch
        {
            "project_number" => project.ProjectNumber,
            "project_name" => "新名称",
            "stage" => "UnderConstruction",
            "contract_signing_status" => "NotSigned",
            "affiliation_type" => "SelfOperated",
            "is_active" => "true",
            "notes" => null,
            "_system_id" or "_project_system_id" => project.Id.ToString(),
            "_concurrency_stamp" => project.ConcurrencyStamp.ToString(),
            "_dataset_version" => ProjectWorkbookVersions.Dataset,
            _ => null
        }).ToArray();
        var workbook = CreateWorkbook((ProjectWorkbookSheet.ProjectMaster, [values]));

        var preview = await fixture.Service.PreviewAsync(new ProjectWorkbookImportRequest("admin", "更新.xlsx", workbook, ImportMode.Update, Actor: ProjectWorkbookActor.Administrator("admin")), CancellationToken.None);
        await fixture.Service.ConfirmAsync(ProjectWorkbookActor.Administrator("admin"), preview.BatchId, CancellationToken.None);

        var updated = await fixture.Db.Projects.SingleAsync();
        updated.Name.Should().Be("新名称");
        updated.Notes.Should().BeNull();
        updated.ConcurrencyStamp.Should().NotBe(originalStamp);
    }

    [Fact]
    public async Task ProjectStageImportResynchronizesExistingQuantityPosting()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var company = new LegalEntity { Code = "STAGE-C", Name = "阶段公司", ShortName = "阶段" };
        var partner = new BusinessPartner { PartnerNumber = "STAGE-PARTNER", Name = "阶段客户", ShortName = "客户" };
        var project = new Project { ProjectNumber = "STAGE-P", Name = "阶段导入项目", Stage = ProjectStage.PartiallySettled };
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
        var contract = new Contract { Project = project, BusinessPartner = partner, ContractNumber = "STAGE-C-01", Name = "阶段合同" };
        var line = new ContractLineItem { Contract = contract, Code = "001", Name = "工程量", Unit = "项", Quantity = 2m, UnitPrice = 50m, RequiresInvoice = true };
        contract.LineItems.Add(line);
        project.Contracts.Add(contract);
        fixture.Db.AddRange(company, partner, project);
        await fixture.Db.SaveChangesAsync();
        var ledgerActor = new CentralLedgerActor("admin", "管理员", new HashSet<Guid> { company.Id }, new HashSet<Guid> { project.Id }, true, false, false, false);
        await new FinancePostingService(fixture.Db).UpsertProjectQuantityReceivableAsync(ledgerActor, line.Id, CancellationToken.None);
        var originalPosting = await fixture.Db.FinanceSettlements.SingleAsync(item => item.SourceType == LedgerSourceType.ProjectQuantity);
        var definition = ProjectWorkbookCatalog.Get(ProjectWorkbookSheet.ProjectMaster);
        var values = definition.Fields.Select(field => field.Key switch
        {
            "project_number" => project.ProjectNumber,
            "project_name" => project.Name,
            "stage" => "UnderConstruction",
            "contract_signing_status" => "NotSigned",
            "affiliation_type" => "SelfOperated",
            "is_active" => "true",
            "legal_entity_ids" => company.Id.ToString(),
            "_system_id" or "_project_system_id" => project.Id.ToString(),
            "_concurrency_stamp" => project.ConcurrencyStamp.ToString(),
            "_dataset_version" => ProjectWorkbookVersions.Dataset,
            _ => null
        }).ToArray();
        var workbook = CreateWorkbook(
            (ProjectWorkbookSheet.ProjectMaster, [values]),
            (ProjectWorkbookSheet.Receivables, [Row(ProjectWorkbookSheet.Receivables, new Dictionary<string, object?>
            {
                ["project_number"] = project.ProjectNumber,
                ["legal_entity_code"] = company.Code,
                ["partner_number"] = partner.PartnerNumber,
                ["source_type"] = "ProjectQuantity",
                ["settlement_state"] = "Final",
                ["entry_date"] = originalPosting.BusinessDate,
                ["original_amount"] = originalPosting.OriginalAmount,
                ["original_invoice_amount"] = originalPosting.OriginalInvoiceAmount,
                ["amount"] = originalPosting.OriginalAmount,
                ["is_voided"] = false,
                ["_system_id"] = originalPosting.Id.ToString(),
                ["_project_system_id"] = project.Id.ToString(),
                ["_concurrency_stamp"] = originalPosting.ConcurrencyStamp.ToString(),
                ["_dataset_version"] = ProjectWorkbookVersions.Dataset
            })]));

        var preview = await fixture.Service.PreviewAsync(new ProjectWorkbookImportRequest("admin", "阶段回退.xlsx", workbook, ImportMode.Update, Actor: ProjectWorkbookActor.Administrator("admin")), CancellationToken.None);
        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(ProjectWorkbookActor.Administrator("admin"), preview.BatchId, CancellationToken.None);

        var storedProject = await fixture.Db.Projects.AsNoTracking().SingleAsync(item => item.Id == project.Id);
        storedProject.Stage.Should().Be(ProjectStage.UnderConstruction);
        var posting = await fixture.Db.FinanceSettlements.AsNoTracking().SingleAsync(item => item.SourceType == LedgerSourceType.ProjectQuantity);
        posting.SettlementState.Should().Be(LedgerSettlementState.Provisional);
        posting.OriginalAmount.Should().Be(100m);
    }

    [Fact]
    public async Task AnyCrossSheetErrorBlocksTheWholeBatch()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var workbook = CreateWorkbook(
            (ProjectWorkbookSheet.ProjectMaster, [Row(ProjectWorkbookSheet.ProjectMaster, new Dictionary<string, object?>
            {
                ["project_number"] = "ROLLBACK-P", ["project_name"] = "不应保存", ["stage"] = "UnderConstruction",
                ["contract_signing_status"] = "NotSigned", ["affiliation_type"] = "SelfOperated", ["is_active"] = true
            })]),
            (ProjectWorkbookSheet.QuantityLines, [Row(ProjectWorkbookSheet.QuantityLines, new Dictionary<string, object?>
            {
                ["project_number"] = "ROLLBACK-P", ["contract_number"] = "MISSING-C", ["code"] = "001", ["name"] = "错误工程量",
                ["unit"] = "项", ["estimated_quantity"] = 1m, ["estimated_unit_price"] = 1m
            })]));

        var preview = await fixture.Service.PreviewAsync(new ProjectWorkbookImportRequest("admin", "错误.xlsx", workbook, Actor: ProjectWorkbookActor.Administrator("admin")), CancellationToken.None);
        var confirm = () => fixture.Service.ConfirmAsync(ProjectWorkbookActor.Administrator("admin"), preview.BatchId, CancellationToken.None);

        preview.Errors.Should().NotBeEmpty();
        preview.Sheets.Single(item => item.Sheet == ProjectWorkbookSheet.QuantityLines).NewRows.Should().Be(0);
        await confirm.Should().ThrowAsync<InvalidOperationException>();
        (await fixture.Db.Projects.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ArbitraryExcelMappingCanPreserveBlankWhenExplicitlyConfigured()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        fixture.Db.Projects.Add(new Project { ProjectNumber = "MAP-P", Name = "保留名称", Notes = "保留备注" });
        await fixture.Db.SaveChangesAsync();
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet("旧项目表", ["工程编号", "工程名称", "备注"], [["MAP-P", "新名称", null]]);
        var mappings = new Dictionary<ProjectWorkbookSheet, IReadOnlyDictionary<string, string>>
        {
            [ProjectWorkbookSheet.ProjectMaster] = new Dictionary<string, string>
            {
                ["工程编号"] = "project_number", ["工程名称"] = "project_name", ["备注"] = "notes"
            }
        };

        var preview = await fixture.Service.PreviewAsync(new ProjectWorkbookImportRequest("admin", "旧项目.xlsx", workbook.ToArray(), ImportMode.Update, Mappings: mappings, BlankMeansNoChange: true, Actor: ProjectWorkbookActor.Administrator("admin")), CancellationToken.None);
        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(ProjectWorkbookActor.Administrator("admin"), preview.BatchId, CancellationToken.None);

        var project = await fixture.Db.Projects.SingleAsync();
        project.Name.Should().Be("新名称");
        project.Notes.Should().Be("保留备注");
    }

    [Fact]
    public async Task ZipImportRejectsAttachmentChecksumMismatchBeforeWriting()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var workbook = CreateWorkbook((ProjectWorkbookSheet.ProjectMaster, [Row(ProjectWorkbookSheet.ProjectMaster, new Dictionary<string, object?>
        {
            ["project_number"] = "ZIP-P", ["project_name"] = "压缩项目", ["stage"] = "UnderConstruction", ["contract_signing_status"] = "NotSigned", ["affiliation_type"] = "SelfOperated", ["is_active"] = true
        })]));
        await using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var workbookEntry = archive.CreateEntry("project-workbook.xlsx");
            await using (var target = workbookEntry.Open()) await target.WriteAsync(workbook);
            var manifest = archive.CreateEntry("manifest.json");
            await using (var writer = new StreamWriter(manifest.Open(), Encoding.UTF8, leaveOpen: false)) await writer.WriteAsync("{\"workbookVersion\":\"project-workbook/1\",\"attachments\":[{\"id\":\"00000000-0000-0000-0000-000000000001\",\"path\":\"attachments/a.txt\",\"sizeBytes\":3,\"sha256\":\"BAD\"}]}");
            var attachment = archive.CreateEntry("attachments/a.txt");
            await using var attachmentStream = attachment.Open();
            await attachmentStream.WriteAsync("abc"u8.ToArray());
        }

        var preview = await fixture.Service.PreviewAsync(new ProjectWorkbookImportRequest("admin", "项目.zip", stream.ToArray(), IncludeAttachments: true, Actor: ProjectWorkbookActor.Administrator("admin")), CancellationToken.None);

        preview.Errors.Should().Contain(item => item.Message.Contains("SHA-256", StringComparison.OrdinalIgnoreCase));
        (await fixture.Db.Projects.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AttachmentSheetWithoutZipIsRejectedDuringPreview()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var workbook = CreateWorkbook(
            (ProjectWorkbookSheet.ProjectMaster, [Row(ProjectWorkbookSheet.ProjectMaster, new Dictionary<string, object?>
            {
                ["project_number"] = "ATT-SHEET-P", ["project_name"] = "附件清单项目", ["stage"] = "UnderConstruction",
                ["contract_signing_status"] = "NotSigned", ["affiliation_type"] = "SelfOperated", ["is_active"] = true
            })]),
            (ProjectWorkbookSheet.Attachments, [Row(ProjectWorkbookSheet.Attachments, new Dictionary<string, object?>
            {
                ["project_number"] = "ATT-SHEET-P", ["relation_type"] = "项目", ["original_file_name"] = "a.pdf",
                ["category"] = "General", ["relative_path"] = "attachments/a.pdf", ["sha256"] = new string('A', 64),
                ["_system_id"] = Guid.NewGuid().ToString()
            })]));

        var preview = await fixture.Service.PreviewAsync(new ProjectWorkbookImportRequest(
            "admin", "附件清单.xlsx", workbook, Actor: ProjectWorkbookActor.Administrator("admin")), CancellationToken.None);

        preview.Errors.Should().Contain(item => item.Message.Contains("ZIP", StringComparison.OrdinalIgnoreCase));
        preview.ErrorRows.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task StageResultStaleConcurrencyStampBlocksPreview()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var project = new Project { ProjectNumber = "CONC-STAGE-P", Name = "阶段成果并发项目", Stage = ProjectStage.UnderConstruction };
        var stage = new StageResult
        {
            Project = project,
            Title = "阶段成果",
            ResultType = StageResultType.Progress,
            Status = StageResultStatus.Draft,
            ResultDate = new DateOnly(2026, 7, 1)
        };
        fixture.Db.AddRange(project, stage);
        await fixture.Db.SaveChangesAsync();
        var workbook = CreateWorkbook((ProjectWorkbookSheet.StageResults, [Row(ProjectWorkbookSheet.StageResults, new Dictionary<string, object?>
        {
            ["project_number"] = project.ProjectNumber, ["title"] = stage.Title, ["result_type"] = stage.ResultType.ToString(),
            ["status"] = stage.Status.ToString(), ["result_date"] = stage.ResultDate, ["_system_id"] = stage.Id.ToString(),
            ["_project_system_id"] = project.Id.ToString(), ["_concurrency_stamp"] = Guid.NewGuid().ToString(),
            ["_dataset_version"] = ProjectWorkbookVersions.Dataset
        })]));

        var preview = await fixture.Service.PreviewAsync(new ProjectWorkbookImportRequest(
            "admin", "阶段成果.xlsx", workbook, ImportMode.Update, Actor: ProjectWorkbookActor.Administrator("admin")), CancellationToken.None);

        preview.Errors.Should().Contain(item => item.ColumnName.Contains("并发", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FullStandardExportCanBePreviewedAgainAndIgnoresReadOnlySummary()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var project = new Project { ProjectNumber = "ROUNDTRIP-P", Name = "往返项目", Stage = ProjectStage.UnderConstruction };
        fixture.Db.Projects.Add(project);
        await fixture.Db.SaveChangesAsync();

        var exporter = await fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(new ProjectListActor("admin", true), new ProjectListQuery("ROUNDTRIP-P", [], null, null, null, null, null, false), false, [project.Id]),
            [], Actor: ProjectWorkbookActor.Administrator("admin")), CancellationToken.None);

        var preview = await fixture.Service.PreviewAsync(
            new ProjectWorkbookImportRequest("admin", "项目管理工作簿.xlsx", exporter.Content, ImportMode.Update, Actor: ProjectWorkbookActor.Administrator("admin")),
            CancellationToken.None);

        preview.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ConfirmRejectsBatchOwnedByAnotherUserAndRejectsSecondConfirmation()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var workbook = CreateWorkbook((ProjectWorkbookSheet.ProjectMaster, [Row(ProjectWorkbookSheet.ProjectMaster, new Dictionary<string, object?>
        {
            ["project_number"] = "OWNER-P", ["project_name"] = "所有者项目", ["stage"] = "UnderConstruction",
            ["contract_signing_status"] = "NotSigned", ["affiliation_type"] = "SelfOperated", ["is_active"] = true
        })]));

        var preview = await fixture.Service.PreviewAsync(
            new ProjectWorkbookImportRequest("owner", "项目.xlsx", workbook, Actor: ProjectWorkbookActor.Administrator("owner")),
            CancellationToken.None);

        await fixture.Service.Invoking(service => service.ConfirmAsync(ProjectWorkbookActor.Administrator("other"), preview.BatchId, CancellationToken.None))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        await fixture.Service.ConfirmAsync(ProjectWorkbookActor.Administrator("owner"), preview.BatchId, CancellationToken.None);
        await fixture.Service.Invoking(service => service.ConfirmAsync(ProjectWorkbookActor.Administrator("owner"), preview.BatchId, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SystemIdFromAnotherProjectCannotBeUsedWithASecondProjectNumber()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var first = new Project { ProjectNumber = "ID-A", Name = "项目 A", Stage = ProjectStage.UnderConstruction };
        var second = new Project { ProjectNumber = "ID-B", Name = "项目 B", Stage = ProjectStage.UnderConstruction };
        fixture.Db.Projects.AddRange(first, second);
        await fixture.Db.SaveChangesAsync();
        var definition = ProjectWorkbookCatalog.Get(ProjectWorkbookSheet.ProjectMaster);
        var values = definition.Fields.Select(field => field.Key switch
        {
            "project_number" => second.ProjectNumber,
            "project_name" => "不应修改",
            "stage" => second.Stage.ToString(),
            "contract_signing_status" => "NotSigned",
            "affiliation_type" => "SelfOperated",
            "is_active" => "true",
            "_system_id" => first.Id.ToString(),
            "_project_system_id" => first.Id.ToString(),
            "_concurrency_stamp" => first.ConcurrencyStamp.ToString(),
            "_dataset_version" => ProjectWorkbookVersions.Dataset,
            _ => null
        }).ToArray();

        var preview = await fixture.Service.PreviewAsync(
            new ProjectWorkbookImportRequest("admin", "冲突.xlsx", CreateWorkbook((ProjectWorkbookSheet.ProjectMaster, [values])), ImportMode.Update, Actor: ProjectWorkbookActor.Administrator("admin")),
            CancellationToken.None);

        preview.Errors.Should().Contain(item => item.Message.Contains("项目", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InvalidEnumAndBooleanValuesArePreviewErrors()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var workbook = CreateWorkbook((ProjectWorkbookSheet.ProjectMaster, [Row(ProjectWorkbookSheet.ProjectMaster, new Dictionary<string, object?>
        {
            ["project_number"] = "INVALID-P", ["project_name"] = "非法值", ["stage"] = "NotAStage",
            ["contract_signing_status"] = "NotSigned", ["affiliation_type"] = "SelfOperated", ["is_active"] = "maybe"
        })]));

        var preview = await fixture.Service.PreviewAsync(
            new ProjectWorkbookImportRequest("admin", "非法.xlsx", workbook, Actor: ProjectWorkbookActor.Administrator("admin")),
            CancellationToken.None);

        preview.Errors.Should().Contain(item => item.ColumnName.Contains("项目阶段", StringComparison.Ordinal));
        preview.Errors.Should().Contain(item => item.Message.Contains("布尔值", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StandardWorkbookVersionMustBeKnown()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet("_metadata", ["WorkbookVersion", "DatasetVersion"], [["project-workbook/999", ProjectWorkbookVersions.Dataset]], new XlsxWorksheetOptions([], true, true));
        workbook.AddWorksheet(ProjectWorkbookCatalog.Get(ProjectWorkbookSheet.ProjectMaster).WorksheetName,
            ProjectWorkbookCatalog.Get(ProjectWorkbookSheet.ProjectMaster).Fields.Select(item => item.Header).ToArray(), []);

        var preview = await fixture.Service.PreviewAsync(
            new ProjectWorkbookImportRequest("admin", "错误版本.xlsx", workbook.ToArray(), Actor: ProjectWorkbookActor.Administrator("admin")),
            CancellationToken.None);

        preview.Errors.Should().Contain(item => item.Message.Contains("版本", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ZipChecksumFileMustMatchWorkbookAndManifest()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var workbook = CreateWorkbook((ProjectWorkbookSheet.ProjectMaster, [Row(ProjectWorkbookSheet.ProjectMaster, new Dictionary<string, object?>
        {
            ["project_number"] = "CHECKSUM-P", ["project_name"] = "校验项目", ["stage"] = "UnderConstruction",
            ["contract_signing_status"] = "NotSigned", ["affiliation_type"] = "SelfOperated", ["is_active"] = true
        })]));
        await using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var workbookEntry = archive.CreateEntry("project-workbook.xlsx");
            await using (var target = workbookEntry.Open()) await target.WriteAsync(workbook);
            var manifest = archive.CreateEntry("manifest.json");
            await using (var target = manifest.Open()) await target.WriteAsync("{\"workbookVersion\":\"project-workbook/1\",\"attachments\":[]}"u8.ToArray());
            var checksums = archive.CreateEntry("checksums.sha256");
            await using (var target = checksums.Open()) await target.WriteAsync("BAD  project-workbook.xlsx"u8.ToArray());
        }

        var preview = await fixture.Service.PreviewAsync(
            new ProjectWorkbookImportRequest("admin", "校验.zip", stream.ToArray(), IncludeAttachments: true, Actor: ProjectWorkbookActor.Administrator("admin")),
            CancellationToken.None);

        preview.Errors.Should().Contain(item => item.Message.Contains("校验", StringComparison.Ordinal) || item.Message.Contains("SHA-256", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CollectionAndPaymentImportLinkParentsAndKeepAccountTransactionsInSync()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var company = new LegalEntity { Code = "FIN-C", Name = "财务公司", ShortName = "财务" };
        var partner = new BusinessPartner { PartnerNumber = "FIN-P", Name = "财务单位", ShortName = "单位" };
        var project = new Project { ProjectNumber = "FIN-WB", Name = "财务项目", Stage = ProjectStage.UnderConstruction };
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
        var account = new FinancialAccount { LegalEntity = company, AccountName = "原账户", AccountType = FinancialAccountType.Bank };
        var replacementAccount = new FinancialAccount { LegalEntity = company, AccountName = "新账户", AccountType = FinancialAccountType.Bank };
        var receivable = new FinanceSettlement { Scope = LedgerScope.External, Direction = LedgerDirection.Receivable, SettlementState = LedgerSettlementState.Final, SourceType = LedgerSourceType.ProjectQuantity, Project = project, LegalEntity = company, BusinessPartner = partner, BusinessDate = new DateOnly(2026, 7, 1), OriginalAmount = 100m, OriginalInvoiceAmount = 100m };
        var payable = new FinanceSettlement { Scope = LedgerScope.External, Direction = LedgerDirection.Payable, SettlementState = LedgerSettlementState.Final, SourceType = LedgerSourceType.CentralLedger, Project = project, LegalEntity = company, BusinessPartner = partner, BusinessDate = new DateOnly(2026, 7, 1), OriginalAmount = 80m, OriginalInvoiceAmount = 80m };
        fixture.Db.AddRange(company, partner, project, account, replacementAccount, receivable, payable);
        await fixture.Db.SaveChangesAsync();

        var workbook = CreateWorkbook(
            (ProjectWorkbookSheet.Collections, [Row(ProjectWorkbookSheet.Collections, new Dictionary<string, object?>
            {
                ["project_number"] = project.ProjectNumber, ["legal_entity_code"] = company.Code, ["partner_number"] = partner.PartnerNumber,
                ["collection_date"] = new DateOnly(2026, 7, 2), ["account_id"] = account.Id.ToString(),
                ["amount"] = 40m, ["payment_method"] = "票据-线下"
            })]),
            (ProjectWorkbookSheet.Payments, [Row(ProjectWorkbookSheet.Payments, new Dictionary<string, object?>
            {
                ["project_number"] = project.ProjectNumber, ["legal_entity_code"] = company.Code, ["partner_number"] = partner.PartnerNumber,
                ["payable_id"] = payable.Id.ToString(), ["payment_date"] = new DateOnly(2026, 7, 2), ["account_id"] = account.Id.ToString(),
                ["amount"] = 30m, ["payment_method"] = "BankTransfer"
            })]));

        var preview = await fixture.Service.PreviewAsync(new ProjectWorkbookImportRequest("admin", "财务.xlsx", workbook, Actor: ProjectWorkbookActor.Administrator("admin")), CancellationToken.None);
        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(ProjectWorkbookActor.Administrator("admin"), preview.BatchId, CancellationToken.None);

        var collection = await fixture.Db.FinanceCashEntries.Include(item => item.Allocations).SingleAsync(item => item.Direction == LedgerDirection.Receivable);
        var payment = await fixture.Db.FinanceCashEntries.Include(item => item.Allocations).SingleAsync(item => item.Direction == LedgerDirection.Payable);
        collection.Allocations.Single().SettlementId.Should().Be(receivable.Id);
        payment.Allocations.Single().SettlementId.Should().Be(payable.Id);
        (await fixture.Db.AccountTransactions.SingleAsync(item => item.SourceType == AccountTransactionSourceType.Collection)).SourceId.Should().Be(collection.Id);
        (await fixture.Db.AccountTransactions.SingleAsync(item => item.SourceType == AccountTransactionSourceType.Payment)).SourceId.Should().Be(payment.Id);

        var updateWorkbook = CreateWorkbook(
            (ProjectWorkbookSheet.Collections, [Row(ProjectWorkbookSheet.Collections, new Dictionary<string, object?>
            {
                ["project_number"] = project.ProjectNumber, ["legal_entity_code"] = company.Code, ["partner_number"] = partner.PartnerNumber,
                ["collection_date"] = new DateOnly(2026, 7, 3), ["account_id"] = replacementAccount.Id.ToString(),
                ["amount"] = 55m, ["payment_method"] = "承兑汇票-更新", ["_system_id"] = collection.Id.ToString(), ["_project_system_id"] = project.Id.ToString(),
                ["_concurrency_stamp"] = collection.ConcurrencyStamp.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
            })]),
            (ProjectWorkbookSheet.Payments, [Row(ProjectWorkbookSheet.Payments, new Dictionary<string, object?>
            {
                ["project_number"] = project.ProjectNumber, ["legal_entity_code"] = company.Code, ["partner_number"] = partner.PartnerNumber,
                ["payable_id"] = payable.Id.ToString(), ["payment_date"] = new DateOnly(2026, 7, 3), ["account_id"] = replacementAccount.Id.ToString(),
                ["amount"] = 35m, ["payment_method"] = "Cash", ["_system_id"] = payment.Id.ToString(), ["_project_system_id"] = project.Id.ToString(),
                ["_concurrency_stamp"] = payment.ConcurrencyStamp.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
            })]));

        var updatePreview = await fixture.Service.PreviewAsync(new ProjectWorkbookImportRequest("admin", "财务更新.xlsx", updateWorkbook, ImportMode.Update, Actor: ProjectWorkbookActor.Administrator("admin")), CancellationToken.None);
        updatePreview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(ProjectWorkbookActor.Administrator("admin"), updatePreview.BatchId, CancellationToken.None);

        var collectionCash = await fixture.Db.AccountTransactions.SingleAsync(item => item.SourceType == AccountTransactionSourceType.Collection);
        var paymentCash = await fixture.Db.AccountTransactions.SingleAsync(item => item.SourceType == AccountTransactionSourceType.Payment);
        collectionCash.AccountId.Should().Be(replacementAccount.Id);
        collectionCash.TransactionDate.Should().Be(new DateOnly(2026, 7, 3));
        collectionCash.Amount.Should().Be(55m);
        paymentCash.AccountId.Should().Be(replacementAccount.Id);
        paymentCash.Amount.Should().Be(35m);

        var updatedCollection = await fixture.Db.FinanceCashEntries.AsNoTracking().Include(item => item.Allocations).SingleAsync(item => item.Id == collection.Id);
        var updatedPayment = await fixture.Db.FinanceCashEntries.AsNoTracking().Include(item => item.Allocations).SingleAsync(item => item.Id == payment.Id);
        updatedCollection.AccountId.Should().Be(replacementAccount.Id);
        updatedCollection.BusinessDate.Should().Be(new DateOnly(2026, 7, 3));
        updatedCollection.Amount.Should().Be(55m);
        updatedCollection.PaymentMethod.Should().Be("承兑汇票-更新");
        updatedCollection.Allocations.Single().SettlementId.Should().Be(receivable.Id);
        updatedCollection.Allocations.Single().Amount.Should().Be(55m);
        updatedPayment.AccountId.Should().Be(replacementAccount.Id);
        updatedPayment.BusinessDate.Should().Be(new DateOnly(2026, 7, 3));
        updatedPayment.Amount.Should().Be(35m);
        updatedPayment.PaymentMethod.Should().Be("Cash");
        updatedPayment.Allocations.Single().SettlementId.Should().Be(payable.Id);
        updatedPayment.Allocations.Single().Amount.Should().Be(35m);
        (await fixture.Db.CollectionEntries.CountAsync()).Should().Be(0);
        (await fixture.Db.PaymentEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task InvoiceImportWritesCentralInvoiceAndAllocationWithoutOldInvoiceRow()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var company = new LegalEntity { Code = "INV-C", Name = "发票公司", ShortName = "发票" };
        var partner = new BusinessPartner { PartnerNumber = "INV-P", Name = "发票客户", ShortName = "客户" };
        var project = new Project { ProjectNumber = "INV-WB", Name = "发票项目", Stage = ProjectStage.UnderConstruction };
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
        var contract = new Contract { Project = project, BusinessPartner = partner, ContractNumber = "INV-C-01", Name = "发票合同" };
        var line = new ContractLineItem { Contract = contract, Code = "001", Name = "发票工程量", Unit = "项", Quantity = 1m, UnitPrice = 100m, RequiresInvoice = true };
        var settlement = new FinanceSettlement
        {
            Scope = LedgerScope.External, Direction = LedgerDirection.Receivable, SettlementState = LedgerSettlementState.Final,
            SourceType = LedgerSourceType.ProjectQuantity, SourceId = line.Id, Project = project, Contract = contract, ContractLineItem = line, LegalEntity = company, BusinessPartner = partner,
            BusinessDate = new DateOnly(2026, 7, 1), OriginalAmount = 100m, OriginalInvoiceAmount = 100m
        };
        fixture.Db.AddRange(company, partner, project, contract, line, settlement);
        await fixture.Db.SaveChangesAsync();

        var workbook = CreateWorkbook((ProjectWorkbookSheet.Invoices, [Row(ProjectWorkbookSheet.Invoices, new Dictionary<string, object?>
        {
            ["project_number"] = project.ProjectNumber, ["legal_entity_code"] = company.Code, ["partner_number"] = partner.PartnerNumber,
            ["direction"] = "Output", ["invoice_number"] = "INV-2026-001", ["invoice_date"] = new DateOnly(2026, 7, 2),
            ["invoice_type"] = "增值税专用发票", ["tax_rate"] = 0.06m, ["net_amount"] = 60m, ["tax_amount"] = 3.6m,
            ["gross_amount"] = 63.6m, ["status"] = "Active"
        })]));

        var preview = await fixture.Service.PreviewAsync(new ProjectWorkbookImportRequest("admin", "发票.xlsx", workbook, Actor: ProjectWorkbookActor.Administrator("admin")), CancellationToken.None);
        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(ProjectWorkbookActor.Administrator("admin"), preview.BatchId, CancellationToken.None);

        var invoice = await fixture.Db.FinanceInvoices.AsNoTracking().Include(item => item.Allocations).SingleAsync();
        invoice.Direction.Should().Be(LedgerDirection.Receivable);
        invoice.Amount.Should().Be(63.6m);
        invoice.Status.Should().Be(LedgerRecordStatus.Active);
        invoice.Allocations.Single().SettlementId.Should().Be(settlement.Id);
        invoice.Allocations.Single().Amount.Should().Be(63.6m);
        (await fixture.Db.InvoiceEntries.CountAsync()).Should().Be(0);

        var updateWorkbook = CreateWorkbook((ProjectWorkbookSheet.Invoices, [Row(ProjectWorkbookSheet.Invoices, new Dictionary<string, object?>
        {
            ["project_number"] = project.ProjectNumber, ["legal_entity_code"] = company.Code, ["partner_number"] = partner.PartnerNumber,
            ["direction"] = "Output", ["invoice_number"] = "INV-2026-001-A", ["invoice_date"] = new DateOnly(2026, 7, 3),
            ["invoice_type"] = "增值税普通发票", ["tax_rate"] = 0.03m, ["net_amount"] = 70m, ["tax_amount"] = 2.1m,
            ["gross_amount"] = 72.1m, ["status"] = "Active", ["_system_id"] = invoice.Id.ToString(),
            ["_project_system_id"] = project.Id.ToString(), ["_concurrency_stamp"] = invoice.ConcurrencyStamp.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
        })]));
        var updatePreview = await fixture.Service.PreviewAsync(new ProjectWorkbookImportRequest("admin", "发票更新.xlsx", updateWorkbook, ImportMode.Update, Actor: ProjectWorkbookActor.Administrator("admin")), CancellationToken.None);
        updatePreview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(ProjectWorkbookActor.Administrator("admin"), updatePreview.BatchId, CancellationToken.None);

        var updated = await fixture.Db.FinanceInvoices.AsNoTracking().Include(item => item.Allocations).SingleAsync(item => item.Id == invoice.Id);
        updated.InvoiceNumber.Should().Be("INV-2026-001-A");
        updated.InvoiceDate.Should().Be(new DateOnly(2026, 7, 3));
        updated.InvoiceType.Should().Be("增值税普通发票");
        updated.TaxRate.Should().Be(0.03m);
        updated.NetAmount.Should().Be(70m);
        updated.TaxAmount.Should().Be(2.1m);
        updated.Amount.Should().Be(72.1m);
        updated.Allocations.Single().SettlementId.Should().Be(settlement.Id);
        updated.Allocations.Single().Amount.Should().Be(72.1m);
        (await fixture.Db.InvoiceEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeductionImportWritesCentralDeductionAndPreservesInvoiceReductionChoice()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var company = new LegalEntity { Code = "DED-C", Name = "扣款公司", ShortName = "扣款" };
        var partner = new BusinessPartner { PartnerNumber = "DED-P", Name = "扣款单位", ShortName = "单位" };
        var project = new Project { ProjectNumber = "DED-WB", Name = "扣款项目", Stage = ProjectStage.UnderConstruction };
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
        var settlement = new FinanceSettlement
        {
            Scope = LedgerScope.External, Direction = LedgerDirection.Payable, SettlementState = LedgerSettlementState.Final,
            SourceType = LedgerSourceType.CentralLedger, Project = project, LegalEntity = company, BusinessPartner = partner,
            BusinessDate = new DateOnly(2026, 7, 1), OriginalAmount = 100m, OriginalInvoiceAmount = 100m
        };
        fixture.Db.AddRange(company, partner, project, settlement);
        await fixture.Db.SaveChangesAsync();

        var workbook = CreateWorkbook((ProjectWorkbookSheet.Deductions, [Row(ProjectWorkbookSheet.Deductions, new Dictionary<string, object?>
        {
            ["project_number"] = project.ProjectNumber, ["legal_entity_code"] = company.Code, ["partner_number"] = partner.PartnerNumber,
            ["settlement_id"] = settlement.Id.ToString(), ["deduction_date"] = new DateOnly(2026, 7, 2), ["amount"] = 12m,
            ["reduce_invoice_amount"] = true, ["reason"] = "质量扣款", ["status"] = "Active"
        })]));

        var preview = await fixture.Service.PreviewAsync(new ProjectWorkbookImportRequest("admin", "扣款.xlsx", workbook, Actor: ProjectWorkbookActor.Administrator("admin")), CancellationToken.None);
        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(ProjectWorkbookActor.Administrator("admin"), preview.BatchId, CancellationToken.None);

        var deduction = await fixture.Db.FinanceDeductions.AsNoTracking().SingleAsync();
        deduction.SettlementId.Should().Be(settlement.Id);
        deduction.Amount.Should().Be(12m);
        deduction.ReduceInvoiceAmount.Should().BeTrue();
        deduction.Reason.Should().Be("质量扣款");
        deduction.Status.Should().Be(LedgerRecordStatus.Active);
        (await fixture.Db.DeductionEntries.CountAsync()).Should().Be(0);

        var updateWorkbook = CreateWorkbook((ProjectWorkbookSheet.Deductions, [Row(ProjectWorkbookSheet.Deductions, new Dictionary<string, object?>
        {
            ["project_number"] = project.ProjectNumber, ["legal_entity_code"] = company.Code, ["partner_number"] = partner.PartnerNumber,
            ["settlement_id"] = settlement.Id.ToString(), ["deduction_date"] = new DateOnly(2026, 7, 3), ["amount"] = 15m,
            ["reduce_invoice_amount"] = false, ["reason"] = "复核后扣款", ["status"] = "Active", ["_system_id"] = deduction.Id.ToString(),
            ["_project_system_id"] = project.Id.ToString(), ["_concurrency_stamp"] = deduction.ConcurrencyStamp.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
        })]));
        var updatePreview = await fixture.Service.PreviewAsync(new ProjectWorkbookImportRequest("admin", "扣款更新.xlsx", updateWorkbook, ImportMode.Update, Actor: ProjectWorkbookActor.Administrator("admin")), CancellationToken.None);
        updatePreview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(ProjectWorkbookActor.Administrator("admin"), updatePreview.BatchId, CancellationToken.None);

        var updated = await fixture.Db.FinanceDeductions.AsNoTracking().SingleAsync(item => item.Id == deduction.Id);
        updated.BusinessDate.Should().Be(new DateOnly(2026, 7, 3));
        updated.Amount.Should().Be(15m);
        updated.ReduceInvoiceAmount.Should().BeFalse();
        updated.Reason.Should().Be("复核后扣款");
        (await fixture.Db.DeductionEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ProjectMasterRoundTripsResponsibleOrganizationCompaniesAndDates()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var user = new ApplicationUser { Id = "responsible", UserName = "responsible", DisplayName = "负责人" };
        var department = new OrganizationUnit { Code = "D-01", Name = "项目部", UnitType = OrganizationUnitType.Department };
        var branch = new OrganizationUnit { Code = "B-01", Name = "分公司", UnitType = OrganizationUnitType.Branch };
        var company = new LegalEntity { Code = "LE-ROUND", Name = "往返公司", ShortName = "往返" };
        fixture.Db.AddRange(user, department, branch, company);
        await fixture.Db.SaveChangesAsync();
        var workbook = CreateWorkbook((ProjectWorkbookSheet.ProjectMaster, [Row(ProjectWorkbookSheet.ProjectMaster, new Dictionary<string, object?>
        {
            ["project_number"] = "MASTER-P", ["project_name"] = "主档项目", ["responsible_user_id"] = user.Id,
            ["department_id"] = department.Id.ToString(), ["branch_id"] = branch.Id.ToString(), ["legal_entity_ids"] = company.Id.ToString(),
            ["stage"] = "UnderConstruction", ["contract_signing_status"] = "FullySigned", ["affiliation_type"] = "SelfOperated",
            ["actual_start_date"] = new DateOnly(2026, 1, 2), ["actual_completion_date"] = new DateOnly(2026, 6, 30), ["is_active"] = true
        })]));

        var preview = await fixture.Service.PreviewAsync(new ProjectWorkbookImportRequest("admin", "主档.xlsx", workbook, Actor: ProjectWorkbookActor.Administrator("admin")), CancellationToken.None);
        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(ProjectWorkbookActor.Administrator("admin"), preview.BatchId, CancellationToken.None);

        var project = await fixture.Db.Projects.Include(item => item.LegalEntities).SingleAsync();
        project.ResponsibleUserId.Should().Be(user.Id);
        project.DepartmentId.Should().Be(department.Id);
        project.BranchId.Should().Be(branch.Id);
        project.ActualStartDate.Should().Be(new DateOnly(2026, 1, 2));
        project.ActualCompletionDate.Should().Be(new DateOnly(2026, 6, 30));
        project.LegalEntities.Select(item => item.LegalEntityId).Should().Equal(company.Id);
    }

    [Fact]
    public async Task PreviewRejectsDuplicateBusinessKeysAndInvalidFinanceDimensions()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var company = new LegalEntity { Code = "VAL-C", Name = "校验公司", ShortName = "校验" };
        var foreignCompany = new LegalEntity { Code = "VAL-OTHER", Name = "其他公司", ShortName = "其他" };
        var project = new Project { ProjectNumber = "VAL-P", Name = "校验项目", Stage = ProjectStage.UnderConstruction };
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
        var account = new FinancialAccount { LegalEntity = foreignCompany, AccountName = "错误账户", AccountType = FinancialAccountType.Bank };
        fixture.Db.AddRange(company, foreignCompany, project, account);
        await fixture.Db.SaveChangesAsync();
        var projectRow = Row(ProjectWorkbookSheet.ProjectMaster, new Dictionary<string, object?>
        {
            ["project_number"] = "DUP-P", ["project_name"] = "重复项目", ["stage"] = "UnderConstruction",
            ["contract_signing_status"] = "NotSigned", ["affiliation_type"] = "SelfOperated", ["is_active"] = true
        });
        var workbook = CreateWorkbook(
            (ProjectWorkbookSheet.ProjectMaster, [projectRow, projectRow]),
            (ProjectWorkbookSheet.Collections, [Row(ProjectWorkbookSheet.Collections, new Dictionary<string, object?>
            {
                ["project_number"] = project.ProjectNumber, ["legal_entity_code"] = company.Code, ["collection_date"] = new DateOnly(2026, 7, 4),
                ["account_id"] = account.Id.ToString(), ["amount"] = -1m, ["payment_method"] = "BankTransfer"
            })]));

        var preview = await fixture.Service.PreviewAsync(new ProjectWorkbookImportRequest("admin", "校验.xlsx", workbook, Actor: ProjectWorkbookActor.Administrator("admin")), CancellationToken.None);

        preview.Errors.Should().Contain(item => item.Message.Contains("重复", StringComparison.Ordinal));
        preview.Errors.Should().Contain(item => item.Message.Contains("大于零", StringComparison.Ordinal));
        preview.Errors.Should().Contain(item => item.Message.Contains("账户", StringComparison.Ordinal) || item.Message.Contains("签约公司", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AttachmentArchiveUsesBusinessKeysInsteadOfSourceDatabaseIds()
    {
        await using var fixture = await ImportFixture.CreateAsync(includeFileStore: true);
        var project = new Project { ProjectNumber = "ATT-KEY-P", Name = "附件键项目", Stage = ProjectStage.UnderConstruction };
        var contract = new Contract { Project = project, ContractNumber = "ATT-KEY-C", Name = "附件键合同", ContractType = ContractType.MainContract };
        fixture.Db.AddRange(project, contract);
        await fixture.Db.SaveChangesAsync();
        var stored = await fixture.FileStore!.SaveAsync(new MemoryStream("attachment"u8.ToArray()), "附件.pdf", CancellationToken.None);
        fixture.Db.Attachments.Add(new Attachment { Contract = contract, StoredName = stored, OriginalFileName = "附件.pdf", SizeBytes = 10, ContentType = "application/pdf" });
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(new ProjectListActor("admin", true), new ProjectListQuery(project.ProjectNumber, [], null, null, null, null, null, false), false, [project.Id]),
            [ProjectWorkbookSheet.ProjectMaster, ProjectWorkbookSheet.Attachments], IncludeAttachments: true, Actor: ProjectWorkbookActor.Administrator("admin")), CancellationToken.None);

        using var archive = new ZipArchive(new MemoryStream(file.Content), ZipArchiveMode.Read);
        var manifestEntry = archive.GetEntry("manifest.json")!;
        await using var manifestStream = manifestEntry.Open();
        using var reader = new StreamReader(manifestStream, Encoding.UTF8);
        var manifest = await reader.ReadToEndAsync();
        manifest.Should().Contain("projectNumber").And.Contain(project.ProjectNumber).And.Contain("contractNumber").And.Contain(contract.ContractNumber);
        manifest.Should().NotContain("projectId").And.NotContain("contractId").And.NotContain("stageResultId");
    }

    [Fact]
    public async Task NewWorkbookPreservesCrossSheetFinanceIdsAndLinksCollectionsAndPayments()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var company = new LegalEntity { Code = "NEW-C", Name = "新建公司", ShortName = "新建" };
        var partner = new BusinessPartner { PartnerNumber = "NEW-BP", Name = "新建单位", ShortName = "单位" };
        var account = new FinancialAccount { LegalEntity = company, AccountName = "新建账户", AccountType = FinancialAccountType.Bank };
        fixture.Db.AddRange(company, partner, account);
        await fixture.Db.SaveChangesAsync();
        var projectId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var quantityId = Guid.NewGuid();
        var payableId = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var workbook = CreateWorkbook(
            (ProjectWorkbookSheet.ProjectMaster, [Row(ProjectWorkbookSheet.ProjectMaster, new Dictionary<string, object?>
            {
                ["project_number"] = "NEW-P", ["project_name"] = "新建跨表项目", ["legal_entity_ids"] = company.Id.ToString(),
                ["stage"] = "UnderConstruction", ["contract_signing_status"] = "NotSigned", ["affiliation_type"] = "SelfOperated", ["is_active"] = true,
                ["_system_id"] = projectId.ToString(), ["_project_system_id"] = projectId.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
            })]),
            (ProjectWorkbookSheet.Contracts, [Row(ProjectWorkbookSheet.Contracts, new Dictionary<string, object?>
            {
                ["project_number"] = "NEW-P", ["contract_number"] = "NEW-C-01", ["name"] = "新建合同", ["contract_type"] = "MainContract",
                ["allocation_mode"] = "SingleCompany", ["counterparty_name"] = partner.Name, ["total_amount"] = 100m, ["is_active"] = true,
                ["_system_id"] = contractId.ToString(), ["_project_system_id"] = projectId.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
            })]),
            (ProjectWorkbookSheet.QuantityLines, [Row(ProjectWorkbookSheet.QuantityLines, new Dictionary<string, object?>
            {
                ["project_number"] = "NEW-P", ["contract_number"] = "NEW-C-01", ["code"] = "001", ["name"] = "新建工程量", ["unit"] = "项",
                ["quantity"] = 1m, ["unit_price"] = 100m, ["accounting_label"] = "暂估", ["requires_invoice"] = true,
                ["_system_id"] = quantityId.ToString(), ["_project_system_id"] = projectId.ToString(), ["_contract_system_id"] = contractId.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
            })]),
            (ProjectWorkbookSheet.Collections, [Row(ProjectWorkbookSheet.Collections, new Dictionary<string, object?>
            {
                ["project_number"] = "NEW-P", ["contract_number"] = "NEW-C-01", ["legal_entity_code"] = company.Code, ["partner_number"] = partner.PartnerNumber,
                ["collection_date"] = new DateOnly(2026, 7, 6), ["account_id"] = account.Id.ToString(), ["amount"] = 40m, ["payment_method"] = "BankTransfer",
                ["_system_id"] = collectionId.ToString(), ["_project_system_id"] = projectId.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
            })]),
            (ProjectWorkbookSheet.Payables, [Row(ProjectWorkbookSheet.Payables, new Dictionary<string, object?>
            {
                ["project_number"] = "NEW-P", ["legal_entity_code"] = company.Code, ["partner_number"] = partner.PartnerNumber, ["source_type"] = "Manual",
                ["entry_date"] = new DateOnly(2026, 7, 5), ["amount"] = 80m, ["_system_id"] = payableId.ToString(), ["_project_system_id"] = projectId.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
            })]),
            (ProjectWorkbookSheet.Payments, [Row(ProjectWorkbookSheet.Payments, new Dictionary<string, object?>
            {
                ["project_number"] = "NEW-P", ["legal_entity_code"] = company.Code, ["partner_number"] = partner.PartnerNumber, ["payable_id"] = payableId.ToString(),
                ["payment_date"] = new DateOnly(2026, 7, 6), ["account_id"] = account.Id.ToString(), ["amount"] = 30m, ["payment_method"] = "BankTransfer",
                ["_system_id"] = paymentId.ToString(), ["_project_system_id"] = projectId.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
            })]));

        var preview = await fixture.Service.PreviewAsync(new ProjectWorkbookImportRequest("admin", "跨表.xlsx", workbook, Actor: ProjectWorkbookActor.Administrator("admin")), CancellationToken.None);
        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(ProjectWorkbookActor.Administrator("admin"), preview.BatchId, CancellationToken.None);

        var project = await fixture.Db.Projects.SingleAsync();
        var receivable = await fixture.Db.FinanceSettlements.SingleAsync(item => item.Direction == LedgerDirection.Receivable);
        var collection = await fixture.Db.FinanceCashEntries.Include(item => item.Allocations).SingleAsync(item => item.Direction == LedgerDirection.Receivable);
        var payable = await fixture.Db.FinanceSettlements.SingleAsync(item => item.Direction == LedgerDirection.Payable);
        var payment = await fixture.Db.FinanceCashEntries.Include(item => item.Allocations).SingleAsync(item => item.Direction == LedgerDirection.Payable);
        project.Id.Should().Be(projectId);
        receivable.SourceType.Should().Be(LedgerSourceType.ProjectQuantity);
        receivable.SourceId.Should().Be(quantityId);
        collection.Id.Should().Be(collectionId);
        collection.Allocations.Single().SettlementId.Should().Be(receivable.Id);
        payable.Id.Should().Be(payableId);
        payment.Id.Should().Be(paymentId);
        payment.Allocations.Single().SettlementId.Should().Be(payableId);
        (await fixture.Db.ReceivableEntries.CountAsync()).Should().Be(0);
        (await fixture.Db.CollectionEntries.CountAsync()).Should().Be(0);
        (await fixture.Db.PayableEntries.CountAsync()).Should().Be(0);
        (await fixture.Db.PaymentEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ReceivableSheetIsReadOnlyWhilePayableUpdateModifiesCentralSettlement()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var company = new LegalEntity { Code = "SET-C", Name = "结算公司", ShortName = "结算" };
        var partner = new BusinessPartner { PartnerNumber = "SET-P", Name = "结算单位", ShortName = "单位" };
        var project = new Project { ProjectNumber = "SET-WB", Name = "结算项目", Stage = ProjectStage.UnderConstruction };
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });
        var receivable = new FinanceSettlement
        {
            Scope = LedgerScope.External, Direction = LedgerDirection.Receivable, SettlementState = LedgerSettlementState.Provisional,
            SourceType = LedgerSourceType.CentralLedger, Project = project, LegalEntity = company, BusinessPartner = partner,
            BusinessDate = new DateOnly(2026, 7, 1), OriginalAmount = 100m, OriginalInvoiceAmount = 100m, Notes = "原应收"
        };
        var payable = new FinanceSettlement
        {
            Scope = LedgerScope.External, Direction = LedgerDirection.Payable, SettlementState = LedgerSettlementState.Provisional,
            SourceType = LedgerSourceType.CentralLedger, Project = project, LegalEntity = company, BusinessPartner = partner,
            BusinessDate = new DateOnly(2026, 7, 1), OriginalAmount = 80m, OriginalInvoiceAmount = 80m, Notes = "原应付"
        };
        fixture.Db.AddRange(company, partner, project, receivable, payable);
        await fixture.Db.SaveChangesAsync();

        var workbook = CreateWorkbook(
            (ProjectWorkbookSheet.Receivables, [Row(ProjectWorkbookSheet.Receivables, new Dictionary<string, object?>
            {
                ["project_number"] = project.ProjectNumber, ["legal_entity_code"] = company.Code, ["partner_number"] = partner.PartnerNumber,
                ["source_type"] = "Manual", ["settlement_state"] = "Final", ["entry_date"] = new DateOnly(2026, 7, 2),
                ["original_amount"] = 120m, ["original_invoice_amount"] = 110m, ["amount"] = 120m, ["description"] = "更新应收", ["is_voided"] = false,
                ["_system_id"] = receivable.Id.ToString(), ["_project_system_id"] = project.Id.ToString(), ["_concurrency_stamp"] = receivable.ConcurrencyStamp.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
            })]),
            (ProjectWorkbookSheet.Payables, [Row(ProjectWorkbookSheet.Payables, new Dictionary<string, object?>
            {
                ["project_number"] = project.ProjectNumber, ["legal_entity_code"] = company.Code, ["partner_number"] = partner.PartnerNumber,
                ["source_type"] = "Manual", ["settlement_state"] = "Final", ["entry_date"] = new DateOnly(2026, 7, 3),
                ["original_amount"] = 95m, ["original_invoice_amount"] = 90m, ["amount"] = 95m, ["description"] = "更新应付", ["is_voided"] = false,
                ["_system_id"] = payable.Id.ToString(), ["_project_system_id"] = project.Id.ToString(), ["_concurrency_stamp"] = payable.ConcurrencyStamp.ToString(), ["_dataset_version"] = ProjectWorkbookVersions.Dataset
            })]));

        var preview = await fixture.Service.PreviewAsync(new ProjectWorkbookImportRequest("admin", "结算更新.xlsx", workbook, ImportMode.Update, Actor: ProjectWorkbookActor.Administrator("admin")), CancellationToken.None);
        preview.Errors.Should().BeEmpty();
        await fixture.Service.ConfirmAsync(ProjectWorkbookActor.Administrator("admin"), preview.BatchId, CancellationToken.None);

        var updatedReceivable = await fixture.Db.FinanceSettlements.AsNoTracking().SingleAsync(item => item.Id == receivable.Id);
        var updatedPayable = await fixture.Db.FinanceSettlements.AsNoTracking().SingleAsync(item => item.Id == payable.Id);
        updatedReceivable.SettlementState.Should().Be(LedgerSettlementState.Provisional);
        updatedReceivable.BusinessDate.Should().Be(new DateOnly(2026, 7, 1));
        updatedReceivable.OriginalAmount.Should().Be(100m);
        updatedReceivable.OriginalInvoiceAmount.Should().Be(100m);
        updatedReceivable.Notes.Should().Be("原应收");
        updatedPayable.SettlementState.Should().Be(LedgerSettlementState.Final);
        updatedPayable.BusinessDate.Should().Be(new DateOnly(2026, 7, 3));
        updatedPayable.OriginalAmount.Should().Be(95m);
        updatedPayable.OriginalInvoiceAmount.Should().Be(90m);
        updatedPayable.Notes.Should().Be("更新应付");
        (await fixture.Db.ReceivableEntries.CountAsync()).Should().Be(0);
        (await fixture.Db.PayableEntries.CountAsync()).Should().Be(0);
    }

    private static byte[] CreateWorkbook(params (ProjectWorkbookSheet Sheet, IReadOnlyList<IReadOnlyList<object?>> Rows)[] sheets)
    {
        var workbook = new SimpleXlsxWorkbook();
        workbook.AddWorksheet("_metadata", ["WorkbookVersion", "DatasetVersion"], [[ProjectWorkbookVersions.Workbook, ProjectWorkbookVersions.Dataset]], new XlsxWorksheetOptions([], true, true));
        foreach (var (sheet, rows) in sheets)
        {
            var definition = ProjectWorkbookCatalog.Get(sheet);
            workbook.AddWorksheet(definition.WorksheetName, definition.Fields.Select(item => item.Header).ToArray(), rows);
        }
        return workbook.ToArray();
    }

    private static object?[] Row(ProjectWorkbookSheet sheet, IReadOnlyDictionary<string, object?> values) =>
        ProjectWorkbookCatalog.Get(sheet).Fields.Select(field => values.GetValueOrDefault(field.Key)).ToArray();

    private sealed class ImportFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private ImportFixture(SqliteConnection connection, ApplicationDbContext db, IProjectWorkbookService service)
        {
            this.connection = connection;
            Db = db;
            Service = service;
        }
        public ApplicationDbContext Db { get; }
        public IProjectWorkbookService Service { get; }

        public IFileStore? FileStore { get; private init; }

        public static async Task<ImportFixture> CreateAsync(bool includeFileStore = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var projectService = new ProjectService(db);
            var financeService = new FinanceLedgerService(db);
            IFileStore? fileStore = includeFileStore ? new LocalFileStore(Path.Combine(Path.GetTempPath(), "project-workbook-import-tests", Guid.NewGuid().ToString("N"))) : null;
            return new ImportFixture(connection, db, new ProjectWorkbookService(db, projectService, financeService, fileStore)) { FileStore = fileStore };
        }
        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
