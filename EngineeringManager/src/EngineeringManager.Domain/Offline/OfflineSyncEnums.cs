namespace EngineeringManager.Domain.Offline;

public enum OfflineSyncStatus
{
    Synced = 1,
    Failed = 2,
    Conflict = 3
}

public enum OfflineConflictResolution
{
    KeepServer = 1,
    RetryLocalOnServerVersion = 2
}
