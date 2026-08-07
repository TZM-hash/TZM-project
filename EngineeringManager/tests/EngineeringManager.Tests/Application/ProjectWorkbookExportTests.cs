using EngineeringManager.Application.DataExchange;
using EngineeringManager.Application.Projects;
using EngineeringManager.Domain.Finance;
using EngineeringManager.Domain.Organization;
using EngineeringManager.Domain.Projects;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Domain.Security;
using EngineeringManager.Infrastructure.Data;
using EngineeringManager.Infrastructure.DataExchange;
using EngineeringManager.Infrastructure.Finance;
using EngineeringManager.Infrastructure.Projects;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using EngineeringManager.Infrastructure.Files;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace EngineeringManager.Tests.Application;

public sealed class ProjectWorkbookExportTests
{
    [Fact]
    public async Task RoundTripProjectWorkbookUsesStandardDirectoryAndControlColumns()
    {
        await using var fixture = await ProjectWorkbookFixture.CreateAsync();
        var project = AddProject(fixture.Db, "WB-ROUNDTRIP", "往返项目", null);
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(
                new ProjectListActor("administrator", true),
                new ProjectListQuery(project.ProjectNumber, [], null, null, null, null, null, false),
                false,
                [project.Id]),
            [ProjectWorkbookSheet.ProjectMaster],
            Actor: ProjectWorkbookActor.Administrator("administrator"),
            UseRoundTripWorkbook: true), CancellationToken.None);

        var sheets = SimpleXlsxReader.Read(file.Content);
        sheets.Select(item => item.Name).Should().Equal("目录", "数据说明", "项目主档");
        sheets.Single(item => item.Name == "项目主档").Rows[0]
            .Should().Contain("_record_id").And.Contain("_business_key").And.Contain("_row_version");
    }

    [Fact]
    public async Task ExportIntersectsManualSelectionWithAuthorizedMatchingProjects()
    {
        await using var fixture = await ProjectWorkbookFixture.CreateAsync();
        var manager = new ApplicationUser { Id = "workbook-manager", UserName = "workbook-manager", DisplayName = "工作簿负责人" };
        fixture.Db.Users.Add(manager);
        var selected = AddProject(fixture.Db, "WB-001", "选中项目", manager.Id);
        var notSelected = AddProject(fixture.Db, "WB-002", "未选项目", manager.Id);
        var unauthorized = AddProject(fixture.Db, "WB-003", "无权项目", null);
        selected.Contracts.Add(new Contract { Project = selected, ContractNumber = "WB-C-001", Name = "选中合同", ContractType = ContractType.MainContract, TotalAmount = 100m });
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(
                new ProjectListActor(manager.Id, false),
                new ProjectListQuery("项目", [], null, null, null, null, null, false, IncludeInactive: true),
                false,
                [selected.Id, unauthorized.Id]),
            [ProjectWorkbookSheet.ProjectMaster, ProjectWorkbookSheet.Contracts],
            Actor: new ProjectWorkbookActor(manager.Id, [SystemRoles.ProjectManager])), CancellationToken.None);
        var sheets = SimpleXlsxReader.Read(file.Content);

        sheets.Select(item => item.Name).Should().Equal("目录说明", "_metadata", "项目主档", "合同");
        sheets.Single(item => item.Name == "项目主档").Rows.SelectMany(item => item).Should().Contain("WB-001");
        sheets.SelectMany(item => item.Rows).SelectMany(item => item).Should().NotContain("WB-002").And.NotContain("WB-003");
        sheets.Single(item => item.Name == "合同").Rows.SelectMany(item => item).Should().Contain("WB-C-001");
    }

    [Fact]
    public async Task AllMatchingExportIncludesReadOnlySummaryAndInactiveProjects()
    {
        await using var fixture = await ProjectWorkbookFixture.CreateAsync();
        AddProject(fixture.Db, "WB-ACTIVE", "活动项目", null);
        AddProject(fixture.Db, "WB-INACTIVE", "停用项目", null).IsActive = false;
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(
                new ProjectListActor("administrator", true),
                new ProjectListQuery("项目", [], null, null, null, null, null, false, IncludeInactive: true), true),
            [ProjectWorkbookSheet.ProjectMaster, ProjectWorkbookSheet.ProjectSummary],
            Actor: ProjectWorkbookActor.Administrator("administrator")), CancellationToken.None);
        var sheets = SimpleXlsxReader.Read(file.Content);

        sheets.Single(item => item.Name == "项目主档").Rows.Should().HaveCount(3);
        var summary = sheets.Single(item => item.Name == "项目经营汇总");
        summary.Rows.Should().HaveCount(3);
        summary.Rows[0].Should().Contain("合同金额").And.Contain("当前工程金额")
            .And.NotContain("预计金额").And.NotContain("已结算金额").And.NotContain("清单项数量");
        (await fixture.Db.DataExchangeTasks.SingleAsync()).RowCount.Should().Be(4);
    }

    [Fact]
    public async Task AttachmentExportCreatesProjectScopedZipWithManifestAndSheet()
    {
        await using var fixture = await ProjectWorkbookFixture.CreateAsync(includeFileStore: true);
        var selected = AddProject(fixture.Db, "WB-ATTACH", "附件项目", null);
        var other = AddProject(fixture.Db, "WB-OTHER", "其他项目", null);
        await fixture.Db.SaveChangesAsync();
        var selectedFile = await fixture.FileStore!.SaveAsync(new MemoryStream("selected"u8.ToArray()), "合同.pdf", CancellationToken.None);
        var otherFile = await fixture.FileStore.SaveAsync(new MemoryStream("other"u8.ToArray()), "其他.pdf", CancellationToken.None);
        fixture.Db.Attachments.AddRange(
            new Attachment { ProjectId = selected.Id, StoredName = selectedFile, OriginalFileName = "合同.pdf", SizeBytes = 8, ContentType = "application/pdf" },
            new Attachment { ProjectId = other.Id, StoredName = otherFile, OriginalFileName = "其他.pdf", SizeBytes = 5, ContentType = "application/pdf" });
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(new ProjectListActor("administrator", true), new ProjectListQuery("附件项目", [], null, null, null, null, null, false), false, [selected.Id]),
            [ProjectWorkbookSheet.ProjectMaster, ProjectWorkbookSheet.Attachments], IncludeAttachments: true,
            Actor: ProjectWorkbookActor.Administrator("administrator")), CancellationToken.None);

        file.ContentType.Should().Be("application/zip");
        using var archive = new ZipArchive(new MemoryStream(file.Content), ZipArchiveMode.Read);
        archive.GetEntry("project-workbook.xlsx").Should().NotBeNull();
        archive.GetEntry("manifest.json").Should().NotBeNull();
        archive.GetEntry("checksums.sha256").Should().NotBeNull();
        archive.Entries.Should().Contain(item => item.FullName.StartsWith("attachments/", StringComparison.Ordinal));
        var workbook = SimpleXlsxReader.Read(await ReadEntryAsync(archive.GetEntry("project-workbook.xlsx")!));
        workbook.Single(item => item.Name == "附件清单").Rows.SelectMany(item => item).Should().Contain("合同.pdf");
        workbook.SelectMany(item => item.Rows).SelectMany(item => item).Should().NotContain("其他.pdf");
    }

    [Fact]
    public async Task AttachmentSheetCanBeExportedAsExcelWithoutAttachmentArchive()
    {
        await using var fixture = await ProjectWorkbookFixture.CreateAsync();
        var project = AddProject(fixture.Db, "WB-ATTACH-XLSX", "仅清单项目", null);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.Attachments.Add(new Attachment
        {
            ProjectId = project.Id,
            StoredName = "清单.pdf",
            OriginalFileName = "清单.pdf",
            SizeBytes = 8,
            ContentType = "application/pdf"
        });
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(
                new ProjectListActor("administrator", true),
                new ProjectListQuery(project.ProjectNumber, [], null, null, null, null, null, false),
                false,
                [project.Id]),
            [ProjectWorkbookSheet.ProjectMaster, ProjectWorkbookSheet.Attachments],
            IncludeAttachments: false,
            Actor: ProjectWorkbookActor.Administrator("administrator")), CancellationToken.None);

        file.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        file.FileName.Should().EndWith(".xlsx");
        var workbook = SimpleXlsxReader.Read(file.Content);
        workbook.Single(item => item.Name == "附件清单").Rows.SelectMany(item => item).Should().Contain("清单.pdf");
    }

    [Fact]
    public async Task PageListExportMatchesQueryOrderAndPageDisplayValues()
    {
        await using var fixture = await ProjectWorkbookFixture.CreateAsync();
        var older = AddProject(fixture.Db, "WB-LIST-001", "旧项目", null);
        older.ActualStartDate = new DateOnly(2026, 7, 1);
        older.Notes = null;
        var latest = AddProject(fixture.Db, "WB-LIST-003", "最新项目", null);
        latest.ActualStartDate = new DateOnly(2026, 8, 1);
        latest.Notes = "这是一个页面导出测试备注";
        AddProject(fixture.Db, "WB-LIST-002", "中间项目", null);
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(
                new ProjectListActor("administrator", true),
                new ProjectListQuery("WB-LIST", [], null, null, null, null, "ProjectNumber", true, PageSize: 20),
                true),
            [ProjectWorkbookSheet.ProjectMaster],
            Actor: ProjectWorkbookActor.Administrator("administrator"),
            ProjectListColumns: ["project_number", "project_name", "stage", "actual_start_date", "contract_amount", "collection_rate", "notes"]), CancellationToken.None);

        var sheets = SimpleXlsxReader.Read(file.Content);
        sheets.Select(item => item.Name).Should().Equal("项目清单");
        var rows = sheets.Single().Rows;
        rows[0].Should().Equal("项目编号", "项目名称", "阶段", "实际开始日期", "合同金额", "收款率", "备注摘要");
        rows[1][0].Should().Be("WB-LIST-003");
        rows[1][1].Should().Be("最新项目");
        rows[1][2].Should().Be("施工中");
        rows[1][3].Should().Be(new DateOnly(2026, 8, 1));
        rows[1][4].Should().Be(0m);
        rows[1][5].Should().Be(0m);
        rows[1][6].Should().Be("这是一个页面导出测试备注");
        rows[3][0].Should().Be("WB-LIST-001");
        rows[3][6].Should().Be("—");
    }

    [Fact]
    public async Task PageListExportDropsDeprecatedProjectSummaryColumns()
    {
        await using var fixture = await ProjectWorkbookFixture.CreateAsync();
        var project = AddProject(fixture.Db, "WB-LEGACY-COLUMNS", "旧列配置项目", null);
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(
                new ProjectListActor("administrator", true),
                new ProjectListQuery(project.ProjectNumber, [], null, null, null, null, null, false),
                false,
                [project.Id]),
            [ProjectWorkbookSheet.ProjectMaster],
            Actor: ProjectWorkbookActor.Administrator("administrator"),
            ProjectListColumns: ["estimated_amount", "settled_amount", "line_item_count", "contract_amount", "current_project_amount", "settlement_status", "contract_count"]), CancellationToken.None);

        SimpleXlsxReader.Read(file.Content).Single().Rows[0].Should().Equal(
            "合同金额", "当前工程金额", "结算状态", "合同数量");
    }

    [Fact]
    public async Task PageListExportSplitsFinanceProgressIntoIndependentEditableValues()
    {
        await using var fixture = await ProjectWorkbookFixture.CreateAsync();
        var project = AddProject(fixture.Db, "WB-RATES", "费率拆分项目", null);
        var company = new LegalEntity { Code = "WB-RATES-LE", Name = "费率测试公司", ShortName = "费率公司" };
        var partner = new BusinessPartner { PartnerNumber = "WB-RATES-BP", Name = "费率测试单位", ShortName = "费率单位" };
        var account = new FinancialAccount { LegalEntity = company, AccountName = "费率测试账户", AccountType = FinancialAccountType.Bank };
        var contract = new Contract { Project = project, BusinessPartner = partner, ContractNumber = "WB-RATES-C", Name = "费率测试合同", TotalAmount = 100m };
        project.Contracts.Add(contract);
        project.LegalEntities.Add(new ProjectLegalEntity { Project = project, LegalEntity = company, IsPrimary = true });

        var receivable = new FinanceSettlement
        {
            Scope = LedgerScope.External,
            Direction = LedgerDirection.Receivable,
            SettlementState = LedgerSettlementState.Final,
            SourceType = LedgerSourceType.CentralLedger,
            LegalEntity = company,
            BusinessPartner = partner,
            Project = project,
            Contract = contract,
            BusinessDate = new DateOnly(2026, 8, 1),
            OriginalAmount = 100m,
            OriginalInvoiceAmount = 100m
        };
        var payable = new FinanceSettlement
        {
            Scope = LedgerScope.External,
            Direction = LedgerDirection.Payable,
            SettlementState = LedgerSettlementState.Final,
            SourceType = LedgerSourceType.CentralLedger,
            LegalEntity = company,
            BusinessPartner = partner,
            Project = project,
            Contract = contract,
            BusinessDate = new DateOnly(2026, 8, 1),
            OriginalAmount = 80m,
            OriginalInvoiceAmount = 80m
        };
        var collection = new FinanceCashEntry
        {
            Scope = LedgerScope.External,
            Direction = LedgerDirection.Receivable,
            CashType = LedgerCashType.Collection,
            LegalEntity = company,
            BusinessPartner = partner,
            Account = account,
            Project = project,
            Contract = contract,
            BusinessDate = new DateOnly(2026, 8, 2),
            Amount = 40m
        };
        collection.Allocations.Add(new FinanceCashAllocation
        {
            CashEntry = collection,
            Settlement = receivable,
            Project = project,
            Contract = contract,
            Amount = 40m,
            AllocationOrder = 1
        });
        var payment = new FinanceCashEntry
        {
            Scope = LedgerScope.External,
            Direction = LedgerDirection.Payable,
            CashType = LedgerCashType.Payment,
            LegalEntity = company,
            BusinessPartner = partner,
            Account = account,
            Project = project,
            Contract = contract,
            BusinessDate = new DateOnly(2026, 8, 3),
            Amount = 25m
        };
        payment.Allocations.Add(new FinanceCashAllocation
        {
            CashEntry = payment,
            Settlement = payable,
            Project = project,
            Contract = contract,
            Amount = 25m,
            AllocationOrder = 1
        });
        var invoice = new FinanceInvoice
        {
            Scope = LedgerScope.External,
            Direction = LedgerDirection.Receivable,
            LegalEntity = company,
            BusinessPartner = partner,
            Project = project,
            Contract = contract,
            InvoiceNumber = "WB-RATES-INV",
            InvoiceDate = new DateOnly(2026, 8, 4),
            Amount = 30m
        };
        invoice.Allocations.Add(new FinanceInvoiceAllocation
        {
            Invoice = invoice,
            Settlement = receivable,
            Project = project,
            Contract = contract,
            Amount = 30m,
            AllocationOrder = 1
        });
        fixture.Db.AddRange(company, partner, account, receivable, payable, collection, payment, invoice);
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(
                new ProjectListActor("administrator", true),
                new ProjectListQuery(project.ProjectNumber, [], null, null, null, null, null, false),
                false,
                [project.Id]),
            [ProjectWorkbookSheet.ProjectMaster],
            Actor: ProjectWorkbookActor.Administrator("administrator"),
            ProjectListColumns:
            [
                "project_number",
                "collection_rate", "collection_receivable_amount", "collection_collected_amount", "collection_uncollected_amount",
                "payment_rate", "payment_payable_amount", "payment_paid_amount", "payment_unpaid_amount",
                "invoice_rate", "invoice_invoiced_amount", "invoice_uninvoiced_amount"
            ]), CancellationToken.None);

        var rows = SimpleXlsxReader.Read(file.Content).Single().Rows;
        rows[0].Should().Equal(
            "项目编号",
            "收款率", "应收金额", "已收金额", "未收金额",
            "付款率", "应付金额", "已付金额", "未付金额",
            "开票率", "已开票金额", "未开票金额");
        rows[1].Should().Equal(
            "WB-RATES",
            0.4m, 100m, 40m, 60m,
            0.3125m, 80m, 25m, 55m,
            0.3m, 30m, 70m);
    }

    [Fact]
    public async Task PageListExportAppliesReferenceLayoutWithoutChangingSelectedColumnOrder()
    {
        await using var fixture = await ProjectWorkbookFixture.CreateAsync();
        var project = AddProject(fixture.Db, "WB-LAYOUT", "这是一个较长的项目名称，用于验证导出换行", null);
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(
                new ProjectListActor("administrator", true),
                new ProjectListQuery(project.ProjectNumber, [], null, null, null, null, null, false),
                false,
                [project.Id]),
            [ProjectWorkbookSheet.ProjectMaster],
            Actor: ProjectWorkbookActor.Administrator("administrator"),
            ProjectListColumns: ["project_name", "project_number", "contract_amount"]), CancellationToken.None);

        var rows = SimpleXlsxReader.Read(file.Content).Single().Rows;
        rows[0].Should().Equal("项目名称", "项目编号", "合同金额");

        using var archive = new ZipArchive(new MemoryStream(file.Content), ZipArchiveMode.Read);
        var worksheet = XDocument.Load(archive.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        worksheet.Descendants(spreadsheet + "col").Single(column => (string?)column.Attribute("min") == "1")
            .Attribute("width")!.Value.Should().Be("21.847619047619");
        worksheet.Descendants(spreadsheet + "autoFilter").Single().Attribute("ref")!.Value.Should().Be("A1:C3");
        worksheet.Descendants(spreadsheet + "pane").Single().Attribute("state")!.Value.Should().Be("frozen");
    }

    [Fact]
    public async Task PageListExportMatchesReferencePrintLayoutAndAddsEditableTotalRow()
    {
        await using var fixture = await ProjectWorkbookFixture.CreateAsync();
        var first = AddProject(fixture.Db, "WB-REFERENCE-001", "参考格式项目一", null);
        var second = AddProject(fixture.Db, "WB-REFERENCE-002", "参考格式项目二", null);
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(
                new ProjectListActor("administrator", true),
                new ProjectListQuery("WB-REFERENCE", [], null, null, null, null, null, false),
                true),
            [ProjectWorkbookSheet.ProjectMaster],
            Actor: ProjectWorkbookActor.Administrator("administrator"),
            ProjectListColumns:
            [
                "serial_number", "project_name", "general_contractor", "general_contractor_contact",
                "collection_rate", "collection_receivable_amount", "collection_collected_amount",
                "collection_uncollected_amount", "invoice_invoiced_amount", "invoice_uninvoiced_amount", "notes"
            ]), CancellationToken.None);

        var rows = SimpleXlsxReader.Read(file.Content).Single().Rows;
        rows.Should().HaveCount(4);
        rows[0].Should().Equal(
            "序号", "项目名称", "总包单位", "总包联系人 / 电话", "收款率",
            "应收金额", "已收金额", "未收金额", "已开票金额", "未开票金额", "备注摘要");
        rows[^1].Should().BeEquivalentTo(new object?[] { null, "合计", null, null, null, 0m, 0m, 0m, 0m, 0m, null });

        using var archive = new ZipArchive(new MemoryStream(file.Content), ZipArchiveMode.Read);
        var worksheet = XDocument.Load(archive.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var workbook = XDocument.Load(archive.GetEntry("xl/workbook.xml")!.Open());
        var styles = XDocument.Load(archive.GetEntry("xl/styles.xml")!.Open());
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var cellXfCount = int.Parse(styles.Descendants(spreadsheet + "cellXfs").Single().Attribute("count")!.Value, System.Globalization.CultureInfo.InvariantCulture);

        worksheet.Descendants(spreadsheet + "row").Select(row => row.Attribute("ht")!.Value)
            .Should().Equal("30", "25", "25", "25");
        worksheet.Descendants(spreadsheet + "c").Select(cell => int.Parse(cell.Attribute("s")!.Value, System.Globalization.CultureInfo.InvariantCulture))
            .Should().OnlyContain(styleIndex => styleIndex < cellXfCount);
        worksheet.Descendants(spreadsheet + "col").Select(column => column.Attribute("width")!.Value)
            .Should().Equal("5.28571428571429", "21.847619047619", "13.2190476190476", "17.7142857142857", "6.71428571428571", "10", "10", "10", "10.7142857142857", "10.7142857142857", "32.2857142857143");
        worksheet.Descendants(spreadsheet + "pageMargins").Single().Attributes()
            .ToDictionary(attribute => attribute.Name.LocalName, attribute => attribute.Value)
            .Should().Contain(new Dictionary<string, string>
            {
                ["left"] = "0.0388888888888889",
                ["right"] = "0.0388888888888889",
                ["top"] = "0.196527777777778",
                ["bottom"] = "0.196527777777778",
                ["header"] = "0.5",
                ["footer"] = "0.5"
            });
        worksheet.Descendants(spreadsheet + "pageSetup").Single().Attributes()
            .ToDictionary(attribute => attribute.Name.LocalName, attribute => attribute.Value)
            .Should().Contain(new Dictionary<string, string>
            {
                ["paperSize"] = "9",
                ["orientation"] = "landscape",
                ["horizontalDpi"] = "600",
                ["fitToWidth"] = "1",
                ["fitToHeight"] = "0"
            });
        worksheet.Descendants(spreadsheet + "pageSetUpPr").Single()
            .Attribute("fitToPage")!.Value.Should().Be("1");
        worksheet.Descendants(spreadsheet + "sheetView").Single()
            .Attribute("zoomScale")!.Value.Should().Be("115");
        worksheet.Descendants(spreadsheet + "c").Single(cell => (string?)cell.Attribute("r") == "F4")
            .Element(spreadsheet + "f")!.Value.Should().Be("SUM(F2:F3)");
        workbook.Descendants(spreadsheet + "definedName")
            .Any(item => (string?)item.Attribute("name") == "_xlnm.Print_Titles" && item.Value == "'项目清单'!$1:$1")
            .Should().BeTrue();
        styles.Descendants(spreadsheet + "font")
            .Any(font => (string?)font.Element(spreadsheet + "name")?.Attribute("val") == "宋体"
                && (string?)font.Element(spreadsheet + "sz")?.Attribute("val") == "10"
                && font.Element(spreadsheet + "b") is not null
                && (string?)font.Element(spreadsheet + "color")?.Attribute("rgb") == "FF1F4E78")
            .Should().BeTrue();
    }

    [Fact]
    public async Task ProjectListExportLeavesWorksheetEditable()
    {
        await using var fixture = await ProjectWorkbookFixture.CreateAsync();
        var project = AddProject(fixture.Db, "WB-EDITABLE-LIST", "可编辑项目", null);
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(
                new ProjectListActor("administrator", true),
                new ProjectListQuery(project.ProjectNumber, [], null, null, null, null, null, false),
                false,
                [project.Id]),
            [ProjectWorkbookSheet.ProjectMaster],
            Actor: ProjectWorkbookActor.Administrator("administrator"),
            ProjectListColumns: ["project_name"]), CancellationToken.None);

        using var archive = new ZipArchive(new MemoryStream(file.Content), ZipArchiveMode.Read);
        var worksheet = XDocument.Load(archive.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        worksheet.Root!.Element(spreadsheet + "sheetProtection").Should().BeNull();
    }

    [Fact]
    public async Task StandardProjectWorkbookLeavesBusinessSheetsEditable()
    {
        await using var fixture = await ProjectWorkbookFixture.CreateAsync();
        var project = AddProject(fixture.Db, "WB-EDITABLE-STANDARD", "标准可编辑项目", null);
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(
                new ProjectListActor("administrator", true),
                new ProjectListQuery(project.ProjectNumber, [], null, null, null, null, null, false),
                false,
                [project.Id]),
            [ProjectWorkbookSheet.ProjectMaster],
            Actor: ProjectWorkbookActor.Administrator("administrator")), CancellationToken.None);

        using var archive = new ZipArchive(new MemoryStream(file.Content), ZipArchiveMode.Read);
        var worksheet = XDocument.Load(archive.GetEntry("xl/worksheets/sheet3.xml")!.Open());
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        worksheet.Root!.Element(spreadsheet + "sheetProtection").Should().BeNull();
    }

    [Fact]
    public async Task PageListExportCombinesGeneralContractorContactAndPhone()
    {
        await using var fixture = await ProjectWorkbookFixture.CreateAsync();
        var project = AddProject(fixture.Db, "WB-LIST-CONTACT", "联系人项目", null);
        project.GeneralContractorContact = "张三";
        project.GeneralContractorPhone = "13800138000";
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(
                new ProjectListActor("administrator", true),
                new ProjectListQuery(project.ProjectNumber, [], null, null, null, null, null, false),
                false,
                [project.Id]),
            [ProjectWorkbookSheet.ProjectMaster],
            Actor: ProjectWorkbookActor.Administrator("administrator"),
            ProjectListColumns: ["general_contractor_contact"]), CancellationToken.None);

        var rows = SimpleXlsxReader.Read(file.Content).Single().Rows;
        rows[0].Should().Equal("总包联系人 / 电话");
        rows[1].Should().Equal("张三 · 13800138000");
    }

    [Fact]
    public async Task PageListExportPreservesPageOrderForManualSelectionAndColumnOrder()
    {
        await using var fixture = await ProjectWorkbookFixture.CreateAsync();
        var first = AddProject(fixture.Db, "WB-SELECT-001", "第一项目", null);
        var second = AddProject(fixture.Db, "WB-SELECT-002", "第二项目", null);
        var third = AddProject(fixture.Db, "WB-SELECT-003", "第三项目", null);
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(
                new ProjectListActor("administrator", true),
                new ProjectListQuery("WB-SELECT", [], null, null, null, null, "ProjectNumber", true),
                false,
                [first.Id, third.Id]),
            [ProjectWorkbookSheet.ProjectMaster],
            Actor: ProjectWorkbookActor.Administrator("administrator"),
            ProjectListColumns: ["project_name", "project_number"]), CancellationToken.None);

        var rows = SimpleXlsxReader.Read(file.Content).Single().Rows;
        rows[0].Should().Equal("项目名称", "项目编号");
        rows[1].Should().Equal("第三项目", "WB-SELECT-003");
        rows[2].Should().Equal("第一项目", "WB-SELECT-001");
        rows.Should().NotContain(row => row.Contains("第二项目"));
    }

    [Fact]
    public async Task ProjectMasterExportIncludesResponsibleEmployeeIdAndName()
    {
        await using var fixture = await ProjectWorkbookFixture.CreateAsync();
        var employee = new Employee
        {
            EmployeeNumber = "WB-RESP-01",
            Name = "导出负责人",
            EmployeeType = EmployeeType.Formal,
            IsActive = true,
            IsProjectResponsible = true
        };
        var project = AddProject(fixture.Db, "WB-RESP", "负责人导出项目", null);
        project.ResponsibleEmployee = employee;
        fixture.Db.Employees.Add(employee);
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(
                new ProjectListActor("administrator", true),
                new ProjectListQuery(project.ProjectNumber, [], null, null, null, null, null, false),
                false,
                [project.Id]),
            [ProjectWorkbookSheet.ProjectMaster],
            Actor: ProjectWorkbookActor.Administrator("administrator")), CancellationToken.None);

        var rows = SimpleXlsxReader.Read(file.Content).Single(item => item.Name == "项目主档").Rows;
        var employeeIdColumn = Array.IndexOf(rows[0].ToArray(), "负责人职员ID");
        var nameColumn = Array.IndexOf(rows[0].ToArray(), "项目负责人");
        employeeIdColumn.Should().BeGreaterThanOrEqualTo(0);
        nameColumn.Should().BeGreaterThanOrEqualTo(0);
        rows[1][employeeIdColumn].Should().Be(employee.Id.ToString());
        rows[1][nameColumn].Should().Be(employee.Name);
    }

    [Fact]
    public async Task PageListExportIncludesAllMatchingProjectsBeyondOneHundredRows()
    {
        await using var fixture = await ProjectWorkbookFixture.CreateAsync();
        for (var index = 1; index <= 101; index++)
        {
            AddProject(fixture.Db, $"WB-BULK-{index:000}", $"批量项目 {index:000}", null);
        }

        await fixture.Db.SaveChangesAsync();
        var file = await fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(
                new ProjectListActor("administrator", true),
                new ProjectListQuery("WB-BULK", [], null, null, null, null, "ProjectNumber", true, PageSize: 20),
                true),
            [ProjectWorkbookSheet.ProjectMaster],
            Actor: ProjectWorkbookActor.Administrator("administrator"),
            ProjectListColumns: ["project_number"]), CancellationToken.None);

        var rows = SimpleXlsxReader.Read(file.Content).Single().Rows;
        rows.Should().HaveCount(103);
        rows[1][0].Should().Be("WB-BULK-101");
        rows[^2][0].Should().Be("WB-BULK-001");
    }

    [Fact]
    public async Task PageListExportKeepsProjectListAndAttachmentsInOptionalZip()
    {
        await using var fixture = await ProjectWorkbookFixture.CreateAsync(includeFileStore: true);
        var project = AddProject(fixture.Db, "WB-PAGE-ZIP", "页面 ZIP 项目", null);
        await fixture.Db.SaveChangesAsync();
        var storedName = await fixture.FileStore!.SaveAsync(new MemoryStream("page zip"u8.ToArray()), "页面附件.pdf", CancellationToken.None);
        fixture.Db.Attachments.Add(new Attachment
        {
            ProjectId = project.Id,
            StoredName = storedName,
            OriginalFileName = "页面附件.pdf",
            SizeBytes = 8,
            ContentType = "application/pdf"
        });
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(
                new ProjectListActor("administrator", true),
                new ProjectListQuery(project.ProjectNumber, [], null, null, null, null, null, false),
                false,
                [project.Id]),
            [ProjectWorkbookSheet.ProjectMaster],
            IncludeAttachments: true,
            Actor: ProjectWorkbookActor.Administrator("administrator"),
            ProjectListColumns: ["project_number", "project_name"]), CancellationToken.None);

        file.ContentType.Should().Be("application/zip");
        using var archive = new ZipArchive(new MemoryStream(file.Content), ZipArchiveMode.Read);
        var workbook = SimpleXlsxReader.Read(await ReadEntryAsync(archive.GetEntry("project-workbook.xlsx")!));
        workbook.Select(item => item.Name).Should().Equal("项目清单", "附件清单");
        workbook.Single(item => item.Name == "项目清单").Rows[1].Should().Equal("WB-PAGE-ZIP", "页面 ZIP 项目");
        workbook.Single(item => item.Name == "附件清单").Rows.SelectMany(item => item).Should().Contain("页面附件.pdf");
        archive.Entries.Should().Contain(item => item.FullName.StartsWith("attachments/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExportRebuildsProjectScopeFromWorkbookActorRoles()
    {
        await using var fixture = await ProjectWorkbookFixture.CreateAsync();
        var manager = new ApplicationUser { Id = "scoped-manager", UserName = "scoped-manager", DisplayName = "范围负责人" };
        fixture.Db.Users.Add(manager);
        AddProject(fixture.Db, "WB-SCOPED", "授权项目", manager.Id);
        var unauthorized = AddProject(fixture.Db, "WB-OUTSIDE", "越权项目", null);
        await fixture.Db.SaveChangesAsync();

        var action = () => fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(
                new ProjectListActor(manager.Id, true),
                new ProjectListQuery(null, [], null, null, null, null, null, false, IncludeInactive: true),
                false,
                [unauthorized.Id]),
            [ProjectWorkbookSheet.ProjectMaster],
            Actor: new ProjectWorkbookActor(manager.Id, [SystemRoles.ProjectManager])), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("没有可导出的项目。");
    }

    [Fact]
    public async Task AttachmentArchiveChecksumCoversManifest()
    {
        await using var fixture = await ProjectWorkbookFixture.CreateAsync(includeFileStore: true);
        var project = AddProject(fixture.Db, "WB-MANIFEST", "清单校验项目", null);
        await fixture.Db.SaveChangesAsync();
        var storedName = await fixture.FileStore!.SaveAsync(new MemoryStream("manifest"u8.ToArray()), "清单.pdf", CancellationToken.None);
        fixture.Db.Attachments.Add(new Attachment
        {
            ProjectId = project.Id,
            StoredName = storedName,
            OriginalFileName = "清单.pdf",
            SizeBytes = 8,
            ContentType = "application/pdf"
        });
        await fixture.Db.SaveChangesAsync();

        var file = await fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(
                new ProjectListActor("administrator", true),
                new ProjectListQuery(project.ProjectNumber, [], null, null, null, null, null, false),
                false,
                [project.Id]),
            [ProjectWorkbookSheet.ProjectMaster, ProjectWorkbookSheet.Attachments],
            IncludeAttachments: true,
            Actor: ProjectWorkbookActor.Administrator("administrator")), CancellationToken.None);

        using var archive = new ZipArchive(new MemoryStream(file.Content), ZipArchiveMode.Read);
        var manifestBytes = await ReadEntryAsync(archive.GetEntry("manifest.json")!);
        var checksums = Encoding.UTF8.GetString(await ReadEntryAsync(archive.GetEntry("checksums.sha256")!));
        checksums.Should().Contain($"{Convert.ToHexString(SHA256.HashData(manifestBytes))}  manifest.json");
    }

    [Fact]
    public async Task FullWorkbookStillAppliesPerSheetPermissionOverrides()
    {
        await using var fixture = await ProjectWorkbookFixture.CreateAsync();
        var manager = new ApplicationUser { Id = "restricted-manager", UserName = "restricted-manager", DisplayName = "受限项目经理" };
        fixture.Db.Users.Add(manager);
        fixture.Db.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            UserId = manager.Id,
            PermissionKey = PermissionKeys.FinanceRead,
            Effect = PermissionEffect.Deny
        });
        var project = AddProject(fixture.Db, "WB-RESTRICTED", "权限覆盖项目", manager.Id);
        await fixture.Db.SaveChangesAsync();

        var action = () => fixture.Service.ExportAsync(new ProjectWorkbookExportRequest(
            new ProjectWorkbookScope(
                new ProjectListActor(manager.Id, false),
                new ProjectListQuery(project.ProjectNumber, [], null, null, null, null, null, false),
                false,
                [project.Id]),
            [],
            Actor: new ProjectWorkbookActor(manager.Id, [SystemRoles.ProjectManager])), CancellationToken.None);

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private static Project AddProject(ApplicationDbContext db, string number, string name, string? responsibleUserId)
    {
        var project = new Project { ProjectNumber = number, Name = name, ResponsibleUserId = responsibleUserId, Stage = ProjectStage.UnderConstruction };
        db.Projects.Add(project);
        return project;
    }

    private static async Task<byte[]> ReadEntryAsync(ZipArchiveEntry entry)
    {
        await using var stream = entry.Open();
        using var output = new MemoryStream();
        await stream.CopyToAsync(output);
        return output.ToArray();
    }

    private sealed class ProjectWorkbookFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private ProjectWorkbookFixture(SqliteConnection connection, ApplicationDbContext db, IProjectWorkbookService service, IFileStore? fileStore)
        {
            this.connection = connection;
            Db = db;
            Service = service;
            FileStore = fileStore;
        }

        public ApplicationDbContext Db { get; }
        public IProjectWorkbookService Service { get; }
        public IFileStore? FileStore { get; }

        public static async Task<ProjectWorkbookFixture> CreateAsync(bool includeFileStore = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var projectService = new ProjectService(db);
            var financeService = new FinanceLedgerService(db);
            IFileStore? fileStore = includeFileStore ? new LocalFileStore(Path.Combine(Path.GetTempPath(), "project-workbook-tests", Guid.NewGuid().ToString("N"))) : null;
            var service = new ProjectWorkbookService(db, projectService, financeService, fileStore);
            return new ProjectWorkbookFixture(connection, db, service, fileStore);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }

    }
}
