using EngineeringManager.Domain.DataExchange;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class ExportSelectionValidatorTests
{
    private static readonly ExportFieldDefinition[] Fields =
    [
        new("project_number", "项目编号", ExportFieldDataType.Text, true),
        new("project_name", "项目名称", ExportFieldDataType.Text, true),
        new("uncollected_amount", "未收款", ExportFieldDataType.Number, false)
    ];

    [Fact]
    public void SelectedFieldOrderIsPreservedAndEmptySelectionUsesDefaults()
    {
        var selected = ExportSelectionValidator.ResolveFields(Fields, ["uncollected_amount", "project_number"]);
        var defaults = ExportSelectionValidator.ResolveFields(Fields, []);

        selected.Select(item => item.Key).Should().Equal("uncollected_amount", "project_number");
        defaults.Select(item => item.Key).Should().Equal("project_number", "project_name");
    }

    [Theory]
    [InlineData("project_number", "project_number")]
    [InlineData("unknown", "project_name")]
    public void DuplicateOrUnknownFieldsAreRejected(string first, string second)
    {
        var action = () => ExportSelectionValidator.ResolveFields(Fields, [first, second]);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SharedTemplatesRequireAdministratorWhilePersonalTemplatesRequireOwner()
    {
        ExportSelectionValidator.ValidateTemplate(ExportTemplateScope.Personal, "user-1", canPublishShared: false);
        ExportSelectionValidator.ValidateTemplate(ExportTemplateScope.Shared, "admin-1", canPublishShared: true);

        var missingOwner = () => ExportSelectionValidator.ValidateTemplate(ExportTemplateScope.Personal, " ", canPublishShared: false);
        var nonAdminShared = () => ExportSelectionValidator.ValidateTemplate(ExportTemplateScope.Shared, "user-1", canPublishShared: false);
        missingOwner.Should().Throw<ArgumentException>();
        nonAdminShared.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CutoffDateCanBeOmittedOrSpecified()
    {
        ExportSelectionValidator.ValidateCutoffDate(null);
        ExportSelectionValidator.ValidateCutoffDate(new DateOnly(2026, 7, 16));
    }
}
