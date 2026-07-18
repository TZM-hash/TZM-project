using EngineeringManager.Application.Employees;
using FluentAssertions;

namespace EngineeringManager.Tests.Application;

public sealed class EmployeeSensitiveDataMaskerTests
{
    [Fact]
    public void ProjectAndReadOnlyViewsReceiveServerMaskedIdentityAndBankAccount()
    {
        EmployeeSensitiveDataMasker.MaskIdentityNumber("510101199001011234").Should().Be("510***********1234");
        EmployeeSensitiveDataMasker.MaskBankAccountNumber("6222021234567890123").Should().Be("***************0123");
    }

    [Fact]
    public void MissingValuesRemainMissing()
    {
        EmployeeSensitiveDataMasker.MaskIdentityNumber(null).Should().BeNull();
        EmployeeSensitiveDataMasker.MaskBankAccountNumber(" ").Should().BeNull();
    }
}
