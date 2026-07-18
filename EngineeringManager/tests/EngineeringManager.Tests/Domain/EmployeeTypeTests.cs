using System.ComponentModel.DataAnnotations;
using System.Reflection;
using EngineeringManager.Domain.Employees;
using EngineeringManager.Web.Presentation;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class EmployeeTypeTests
{
    [Theory]
    [InlineData(EmployeeType.Formal, 1, "正式员工")]
    [InlineData(EmployeeType.Labor, 2, "劳务员工")]
    [InlineData(EmployeeType.Temporary, 3, "特殊临时人员")]
    public void EmployeeTypesHaveStableValuesAndChineseLabels(EmployeeType employeeType, int persistedValue, string label)
    {
        ((int)employeeType).Should().Be(persistedValue);
        employeeType.ToChinese().Should().Be(label);
        var display = employeeType.GetType()
            .GetMember(employeeType.ToString())
            .Single()
            .GetCustomAttribute<DisplayAttribute>();
        display.Should().NotBeNull();
        display!.Name.Should().Be(label);
    }

    [Fact]
    public void EmployeeCreateAndEditFormUsesEnumDisplayLabels()
    {
        var page = ReadFile("src", "EngineeringManager.Web", "Pages", "Employees", "Create.cshtml");

        page.Should().Contain("GetEnumSelectList<EngineeringManager.Domain.Employees.EmployeeType>()");
    }

    private static string ReadFile(params string[] parts) => File.ReadAllText(Path.Combine(RepositoryRoot(), Path.Combine(parts)));

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EngineeringManager.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Cannot locate EngineeringManager.sln.");
    }
}
