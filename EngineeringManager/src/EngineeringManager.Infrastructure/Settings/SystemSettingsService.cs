using System.Text.Json;
using System.Text.Json.Serialization;
using EngineeringManager.Application.Settings;
using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EngineeringManager.Infrastructure.Settings;

public sealed class SystemSettingsService(ApplicationDbContext db, IMemoryCache cache) : ISystemSettingsService
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private const string CacheKey = "system-display-settings";
    private const string ThemeKey = "Display.Theme";
    private const string MotionKey = "Display.Motion";
    private const string EffectsKey = "Display.Effects";
    private const string FontKey = "Display.Font";
    private const string DensityKey = "Display.TableDensity";

    public async Task<SystemDisplaySettings> GetAsync(CancellationToken token)
    {
        if (cache.TryGetValue(CacheKey, out SystemDisplaySettings? cached) && cached is not null)
        {
            return cached;
        }

        var values = await db.SystemSettings.AsNoTracking().ToDictionaryAsync(item => item.Key, item => item.Value, token);
        var settings = new SystemDisplaySettings(
            Parse(values, ThemeKey, VisualTheme.Default),
            Parse(values, MotionKey, MotionStyle.Technology),
            Parse(values, EffectsKey, UiEffectsLevel.Medium),
            Parse(values, FontKey, GlobalFont.SystemDefault),
            Parse(values, DensityKey, TableDensity.Standard));
        cache.Set(CacheKey, settings, TimeSpan.FromMinutes(5));
        return settings;
    }

    public async Task SaveAsync(SettingsActor actor, SystemDisplaySettings settings, CancellationToken token)
    {
        if (!actor.CanManage)
        {
            throw new UnauthorizedAccessException("只有系统级管理员可以修改全局显示设置。");
        }
        Validate(settings);
        var before = await GetAsync(token);
        var existing = await db.SystemSettings.ToDictionaryAsync(item => item.Key, token);
        Upsert(existing, ThemeKey, settings.Theme.ToString(), actor.UserId);
        Upsert(existing, MotionKey, settings.Motion.ToString(), actor.UserId);
        Upsert(existing, EffectsKey, settings.Effects.ToString(), actor.UserId);
        Upsert(existing, FontKey, settings.Font.ToString(), actor.UserId);
        Upsert(existing, DensityKey, settings.Density.ToString(), actor.UserId);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = actor.UserId,
            UserName = actor.UserName,
            Action = "UpdateSystemDisplaySettings",
            EntityType = "SystemDisplaySettings",
            EntityId = "global",
            Reason = "维护全局主题、动效、字体和表格密度",
            BeforeJson = JsonSerializer.Serialize(before, AuditJsonOptions),
            AfterJson = JsonSerializer.Serialize(settings, AuditJsonOptions)
        });
        await db.SaveChangesAsync(token);
        cache.Remove(CacheKey);
    }

    private void Upsert(Dictionary<string, SystemSetting> existing, string key, string value, string userId)
    {
        if (!existing.TryGetValue(key, out var setting))
        {
            setting = new SystemSetting { Key = key };
            db.SystemSettings.Add(setting);
        }
        setting.Value = value;
        setting.UpdatedAt = DateTimeOffset.UtcNow;
        setting.UpdatedByUserId = userId;
    }

    private static T Parse<T>(Dictionary<string, string> values, string key, T fallback) where T : struct, Enum =>
        values.TryGetValue(key, out var value) && Enum.TryParse<T>(value, true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : fallback;

    private static void Validate(SystemDisplaySettings settings)
    {
        if (!Enum.IsDefined(settings.Theme) || !Enum.IsDefined(settings.Motion) || !Enum.IsDefined(settings.Effects) ||
            !Enum.IsDefined(settings.Font) || !Enum.IsDefined(settings.Density))
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "显示设置包含未知选项。");
        }
    }
}
