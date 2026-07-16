namespace EngineeringManager.Domain.Offline;

public static class OfflinePhotoPolicy
{
    public const int MaximumPhotosPerDraft = 20;
    public const long MaximumPhotoBytes = 3 * 1024 * 1024;

    public static void Validate(int photoCount, long photoBytes)
    {
        if (photoCount < 0 || photoCount > MaximumPhotosPerDraft)
        {
            throw new ArgumentOutOfRangeException(nameof(photoCount), $"每份离线草稿最多保存 {MaximumPhotosPerDraft} 张照片。");
        }

        if (photoBytes <= 0 || photoBytes > MaximumPhotoBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(photoBytes), $"单张离线照片不能超过 {MaximumPhotoBytes} 字节。");
        }
    }
}
