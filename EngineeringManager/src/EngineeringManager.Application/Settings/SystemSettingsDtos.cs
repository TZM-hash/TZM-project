namespace EngineeringManager.Application.Settings;

public enum VisualTheme { Default = 1, ClearGlass = 2 }
public enum MotionStyle { Technology = 1, Apple = 2 }
public enum UiEffectsLevel { Low = 1, Medium = 2, High = 3 }
public enum GlobalFont { SystemDefault = 1, MicrosoftYaHei = 2, MicrosoftJhengHei = 3, ChineseSerif = 4, ChineseKai = 5 }
public enum TableDensity { Compact = 1, Standard = 2, Spacious = 3 }

public sealed record SystemDisplaySettings(
    VisualTheme Theme,
    MotionStyle Motion,
    UiEffectsLevel Effects,
    GlobalFont Font,
    TableDensity Density)
{
    public static SystemDisplaySettings Default { get; } = new(
        VisualTheme.Default,
        MotionStyle.Technology,
        UiEffectsLevel.Medium,
        GlobalFont.SystemDefault,
        TableDensity.Standard);

    public string ThemeCssClass => Theme == VisualTheme.ClearGlass ? "theme-clear-glass" : "theme-default";
    public string MotionCssClass => Motion == MotionStyle.Apple ? "motion-apple" : "motion-technology";
    public string EffectsCssClass => $"ui-effects-{Effects.ToString().ToLowerInvariant()}";
    public string FontCssClass => Font switch
    {
        GlobalFont.MicrosoftYaHei => "font-microsoft-yahei",
        GlobalFont.MicrosoftJhengHei => "font-microsoft-jhenghei",
        GlobalFont.ChineseSerif => "font-chinese-serif",
        GlobalFont.ChineseKai => "font-chinese-kai",
        _ => "font-system-default"
    };
    public string DensityCssClass => Density switch
    {
        TableDensity.Compact => "table-density-compact",
        TableDensity.Spacious => "table-density-spacious",
        _ => "table-density-standard"
    };
}

public sealed record SettingsActor(string UserId, string UserName, bool CanManage);
