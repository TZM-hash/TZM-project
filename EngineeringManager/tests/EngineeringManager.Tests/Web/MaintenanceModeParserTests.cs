using EngineeringManager.Web;
using FluentAssertions;

namespace EngineeringManager.Tests.Web;

public sealed class MaintenanceModeParserTests
{
    [Fact]
    public void ParseRecognizesTheExactLegacyRepairFlag()
    {
        MaintenanceModeParser.Parse(["--repair-legacy-project-data"])
            .Should().Be(MaintenanceMode.LegacyProjectDataRepair);
    }

    [Fact]
    public void ParseRejectsMalformedOrMultipleMaintenanceFlags()
    {
        var malformed = () => MaintenanceModeParser.Parse(["--repair-legacy-project-data=true"]);
        var multiple = () => MaintenanceModeParser.Parse(["--migrate-central-ledger", "--repair-legacy-project-data"]);

        malformed.Should().Throw<ArgumentException>();
        multiple.Should().Throw<ArgumentException>();
    }
}
