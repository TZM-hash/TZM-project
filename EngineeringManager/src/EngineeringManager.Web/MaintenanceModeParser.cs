namespace EngineeringManager.Web;

public enum MaintenanceMode
{
    Web = 0,
    CentralLedgerMigration = 1,
    LegacyProjectDataRepair = 2,
    ProjectDateBackfillFromNotes = 3
}

public static class MaintenanceModeParser
{
    private const string CentralLedgerFlag = "--migrate-central-ledger";
    private const string LegacyRepairFlag = "--repair-legacy-project-data";
    private const string ProjectDateBackfillFlag = "--backfill-project-dates-from-notes";

    public static MaintenanceMode Parse(IReadOnlyCollection<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var selected = new List<MaintenanceMode>();
        foreach (var argument in args)
        {
            if (MatchesFlag(argument, CentralLedgerFlag))
            {
                selected.Add(MaintenanceMode.CentralLedgerMigration);
            }
            else if (MatchesFlag(argument, LegacyRepairFlag))
            {
                selected.Add(MaintenanceMode.LegacyProjectDataRepair);
            }
            else if (MatchesFlag(argument, ProjectDateBackfillFlag))
            {
                selected.Add(MaintenanceMode.ProjectDateBackfillFromNotes);
            }
        }

        if (selected.Count > 1)
            throw new ArgumentException("维护命令参数不能同时指定多个模式。", nameof(args));
        return selected.Count == 0 ? MaintenanceMode.Web : selected[0];
    }

    private static bool MatchesFlag(string argument, string flag)
    {
        if (!argument.StartsWith(flag, StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase))
            return true;
        throw new ArgumentException($"维护命令参数必须使用精确格式：{flag}。", nameof(argument));
    }
}
