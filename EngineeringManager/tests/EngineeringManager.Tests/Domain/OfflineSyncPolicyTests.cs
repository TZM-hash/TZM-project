using EngineeringManager.Domain.Offline;
using FluentAssertions;

namespace EngineeringManager.Tests.Domain;

public sealed class OfflineSyncPolicyTests
{
    [Fact]
    public void PhotoPolicyAcceptsConfiguredBoundary()
    {
        var action = () => OfflinePhotoPolicy.Validate(
            OfflinePhotoPolicy.MaximumPhotosPerDraft,
            OfflinePhotoPolicy.MaximumPhotoBytes);

        action.Should().NotThrow();
    }

    [Theory]
    [InlineData(21, 1)]
    [InlineData(1, 3 * 1024 * 1024 + 1)]
    public void PhotoPolicyRejectsCountOrSizeAboveLimit(int photoCount, long photoBytes)
    {
        var action = () => OfflinePhotoPolicy.Validate(photoCount, photoBytes);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SyncStatusesIncludeFailureAndConflictResolutionChoices()
    {
        Enum.GetValues<OfflineSyncStatus>().Should().Contain([
            OfflineSyncStatus.Synced,
            OfflineSyncStatus.Failed,
            OfflineSyncStatus.Conflict]);
        Enum.GetValues<OfflineConflictResolution>().Should().BeEquivalentTo([
            OfflineConflictResolution.KeepServer,
            OfflineConflictResolution.RetryLocalOnServerVersion]);
    }
}
