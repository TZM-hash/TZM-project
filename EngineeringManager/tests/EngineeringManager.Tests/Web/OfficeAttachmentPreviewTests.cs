using System.IO.Compression;
using System.Text;
using EngineeringManager.Web.Presentation;
using FluentAssertions;
using NPOI.HSSF.UserModel;
using NPOI.POIFS.FileSystem;

namespace EngineeringManager.Tests.Web;

public sealed class OfficeAttachmentPreviewTests
{
    [Fact]
    public void CreatesHtmlPreviewForDocx()
    {
        var bytes = CreateZip(("word/document.xml", """
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>项目合同</w:t></w:r></w:p><w:p><w:r><w:t>付款条款</w:t></w:r></w:p></w:body></w:document>
            """));

        var html = OfficeAttachmentPreview.Create("合同.docx", bytes);

        html.Should().Contain("项目合同").And.Contain("付款条款").And.Contain("text/html");
    }

    [Fact]
    public void CreatesHtmlPreviewForXlsx()
    {
        var bytes = CreateZip(
            ("xl/workbook.xml", """
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="工资表" sheetId="1" r:id="rId1" /></sheets></workbook>
                """),
            ("xl/_rels/workbook.xml.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Target="worksheets/sheet1.xml" /></Relationships>
                """),
            ("xl/sharedStrings.xml", """
                <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><si><t>姓名</t></si><si><t>张三</t></si></sst>
                """),
            ("xl/worksheets/sheet1.xml", """
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData><row r="1"><c r="A1" t="s"><v>0</v></c><c r="B1" t="s"><v>1</v></c></row></sheetData></worksheet>
                """));

        var html = OfficeAttachmentPreview.Create("工资表.xlsx", bytes);

        html.Should().Contain("工资表").And.Contain("姓名").And.Contain("张三").And.Contain("text/html");
    }

    [Fact]
    public void CreatesHtmlPreviewForPptxAndLeavesUnsupportedFormatsNull()
    {
        var bytes = CreateZip(("ppt/slides/slide1.xml", """
            <p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><p:cSld><p:spTree><p:sp><p:txBody><a:p><a:r><a:t>项目汇报</a:t></a:r></a:p></p:txBody></p:sp></p:spTree></p:cSld></p:sld>
            """));

        OfficeAttachmentPreview.Create("汇报.pptx", bytes).Should().Contain("项目汇报");
        OfficeAttachmentPreview.Create("不支持格式.odt", bytes).Should().BeNull();
    }

    [Fact]
    public void CreatesHtmlPreviewForLegacyDoc()
    {
        var html = OfficeAttachmentPreview.Create("旧版.doc", LoadFixture("legacy-word.doc"));

        html.Should().Contain("Line 1").And.Contain("Line 2").And.Contain("text/html");
    }

    [Fact]
    public void CreatesHtmlPreviewForLegacyXls()
    {
        var workbook = new HSSFWorkbook();
        var sheet = workbook.CreateSheet("工资表");
        sheet.CreateRow(0).CreateCell(0).SetCellValue("姓名");
        sheet.GetRow(0).CreateCell(1).SetCellValue("张三");
        using var stream = new MemoryStream();
        workbook.Write(stream);

        var html = OfficeAttachmentPreview.Create("工资表.xls", stream.ToArray());

        html.Should().Contain("工资表").And.Contain("姓名").And.Contain("张三");
    }

    [Fact]
    public void CreatesHtmlPreviewForLegacyPpt()
    {
        var payload = Encoding.Unicode.GetBytes("项目汇报");
        var record = new byte[8 + payload.Length];
        BitConverter.GetBytes((ushort)0).CopyTo(record, 0);
        BitConverter.GetBytes((ushort)0x0FA0).CopyTo(record, 2);
        BitConverter.GetBytes(payload.Length).CopyTo(record, 4);
        payload.CopyTo(record, 8);
        var fileSystem = new POIFSFileSystem();
        fileSystem.Root.CreateDocument("PowerPoint Document", new MemoryStream(record));
        using var stream = new MemoryStream();
        fileSystem.WriteFileSystem(stream);

        var html = OfficeAttachmentPreview.Create("汇报.ppt", stream.ToArray());

        html.Should().Contain("项目汇报");
    }

    private static byte[] LoadFixture(string fileName)
    {
        var path = Path.Combine(RepositoryRoot(), "tests", "EngineeringManager.Tests", "Fixtures", fileName);
        return File.ReadAllBytes(path);
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EngineeringManager.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }

    private static byte[] CreateZip(params (string Path, string Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }
}
