using EngineeringManager.Infrastructure.Development;
using FluentAssertions;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class DevelopmentSampleDataSeederTests
{
    [Theory]
    [InlineData("Production", "EngineeringManager_Test")]
    [InlineData("Development", "EngineeringManager")]
    [InlineData("Development", "ProductionDb")]
    public void SafetyGuardRejectsNonDevelopmentOrNonTestDatabase(string environment, string database)
    {
        var action = () => DevelopmentSampleDataSeeder.ValidateSafety(environment, database);
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SafetyGuardAllowsExplicitDevelopmentTestDatabase()
    {
        var action = () => DevelopmentSampleDataSeeder.ValidateSafety("Development", "EngineeringManager_Test");
        action.Should().NotThrow();
    }

    [Fact]
    public void GeneratedPasswordIsEasyToTypeAndMeetsIdentityRules()
    {
        var first = DevelopmentSampleDataSeeder.GenerateTestPassword();
        var second = DevelopmentSampleDataSeeder.GenerateTestPassword();
        first.Should().StartWith("TestAdmin").And.HaveLength(13);
        first.Should().MatchRegex("^[A-Za-z0-9]+$");
        first.Should().NotBe(second);
    }
}
