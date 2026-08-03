namespace EngineeringManager.Application.Settings;

public enum VisualTheme
{
    Default = 1,
    ClearGlass = 2,
    LavenderCream = 3
}
public enum UiAppearanceStyle
{
    Classic = 1,
    RoundedSoft = 2
}
public enum MotionStyle { Technology = 1, Apple = 2 }
public enum UiEffectsLevel { Low = 1, Medium = 2, High = 3 }
public enum GlobalFont { SystemDefault = 1, MicrosoftYaHei = 2, MicrosoftJhengHei = 3, ChineseSerif = 4, ChineseKai = 5 }
public enum TableDensity { Compact = 1, Standard = 2, Spacious = 3 }
public enum GlobalFontSize { Small = 1, Standard = 2, Large = 3, ExtraLarge = 4 }

public sealed record SystemDisplaySettings(
    VisualTheme Theme,
    MotionStyle Motion,
    UiEffectsLevel Effects,
    GlobalFont Font,
    TableDensity Density,
    GlobalFontSize FontSize,
    UiAppearanceStyle Appearance = UiAppearanceStyle.Classic)
{
    public static SystemDisplaySettings Default { get; } = new(
        VisualTheme.Default,
        MotionStyle.Technology,
        UiEffectsLevel.Medium,
        GlobalFont.SystemDefault,
        TableDensity.Standard,
        GlobalFontSize.Standard,
        UiAppearanceStyle.Classic);

    public string ThemeCssClass => Theme switch
    {
        VisualTheme.ClearGlass => "theme-clear-glass",
        VisualTheme.LavenderCream => "theme-lavender-cream",
        _ => "theme-default"
    };
    public string ThemeColor => Theme == VisualTheme.LavenderCream ? "#7653d6" : "#2563eb";
    public string AppearanceCssClass => Appearance switch
    {
        UiAppearanceStyle.RoundedSoft => "appearance-rounded-soft",
        _ => "appearance-classic"
    };
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
    public string FontSizeCssClass => FontSize switch
    {
        GlobalFontSize.Small => "font-size-small",
        GlobalFontSize.Large => "font-size-large",
        GlobalFontSize.ExtraLarge => "font-size-extra-large",
        _ => "font-size-standard"
    };
}

public sealed record SettingsActor(string UserId, string UserName, bool CanManage);
