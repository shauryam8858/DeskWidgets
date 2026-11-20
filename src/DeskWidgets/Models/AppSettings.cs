namespace DeskWidgets.Models;

public static class ThemeNames
{
    public const string DarkGlass = "DarkGlass";
    public const string LightGlass = "LightGlass";
    public const string MidnightBlue = "MidnightBlue";

    public static readonly string[] All = { DarkGlass, LightGlass, MidnightBlue };
}

public static class PositionPresets
{
    public const string TopLeft = "TopLeft";
    public const string TopCentre = "TopCentre";
    public const string TopRight = "TopRight";
    public const string MiddleLeft = "MiddleLeft";
    public const string Centre = "Centre";
    public const string MiddleRight = "MiddleRight";
    public const string BottomLeft = "BottomLeft";
    public const string BottomCentre = "BottomCentre";
    public const string BottomRight = "BottomRight";
    public const string Custom = "Custom";

    public static readonly string[] All =
    {
        TopLeft, TopCentre, TopRight, MiddleLeft, Centre, MiddleRight,
        BottomLeft, BottomCentre, BottomRight, Custom
    };
}

public static class DesktopModes
{
    /// <summary>Above the desktop icons, below normal windows. Default and safest.</summary>
    public const string Desktop = "Desktop";
    /// <summary>Experimental: child of the WorkerW window, behind desktop icons.</summary>
    public const string WorkerW = "WorkerW";
    /// <summary>An ordinary window without a taskbar button.</summary>
    public const string Normal = "Normal";
    public const string AlwaysOnTop = "AlwaysOnTop";

    public static readonly string[] All = { Desktop, WorkerW, Normal, AlwaysOnTop };
}

public static class Limits
{
    public const int MinWidth = 300;
    public const int MinHeight = 440;
    public const int MaxWidth = 900;
    public const int MaxHeight = 900;
    public const int MaxCornerRadius = 40;
    public const int MaxMargin = 200;
    public const int MinCoordinate = -20000;
    public const int MaxCoordinate = 20000;
}

/// <summary>
/// Everything that is saved to settings.json. Width and Height are the size of the
/// visible panel in 96-DPI units (the transparent shadow border is added on top).
/// </summary>
public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    // Appearance
    public string Theme { get; set; } = ThemeNames.DarkGlass;
    public string AccentColor { get; set; } = "#6C8EBF";
    public bool UseCustomAccent { get; set; }
    public string BackgroundColor { get; set; } = "#1E2229";
    public bool UseCustomBackground { get; set; }
    public int OpacityPercent { get; set; } = 85;
    public int CornerRadius { get; set; } = 20;
    public bool Shadow { get; set; } = true;
    public string FontFamily { get; set; } = "Segoe UI";

    // Position and layout
    public string PositionPreset { get; set; } = PositionPresets.MiddleRight;
    public int? X { get; set; }
    public int? Y { get; set; }
    public int Margin { get; set; } = 24;
    public bool LockPosition { get; set; }
    public int Width { get; set; } = 360;
    public int Height { get; set; } = 560;
    public bool Visible { get; set; } = true;
    public string DesktopMode { get; set; } = DesktopModes.Desktop;

    // Clock and calendar
    public bool Use24Hour { get; set; }
    public bool ShowSeconds { get; set; } = true;
    public bool ShowDate { get; set; } = true;
    public bool WeekStartsOnMonday { get; set; }

    // Countdown (no default date: the user chooses it)
    public string CountdownName { get; set; } = "";
    public DateTime? CountdownDate { get; set; }

    // General
    public bool StartWithWindows { get; set; }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();

    /// <summary>Repairs any invalid value so a hand-edited or damaged file can never break the app.</summary>
    public void Sanitize()
    {
        Theme = Array.IndexOf(ThemeNames.All, Theme) >= 0 ? Theme : ThemeNames.DarkGlass;
        if (!HexColor.TryParse(AccentColor, out _)) AccentColor = "#6C8EBF";
        if (!HexColor.TryParse(BackgroundColor, out _)) BackgroundColor = "#1E2229";
        OpacityPercent = Math.Clamp(OpacityPercent, 0, 100);
        CornerRadius = Math.Clamp(CornerRadius, 0, Limits.MaxCornerRadius);
        if (string.IsNullOrWhiteSpace(FontFamily) || FontFamily.Length > 64) FontFamily = "Segoe UI";
        PositionPreset = Array.IndexOf(PositionPresets.All, PositionPreset) >= 0 ? PositionPreset : PositionPresets.MiddleRight;
        if (X.HasValue) X = Math.Clamp(X.Value, Limits.MinCoordinate, Limits.MaxCoordinate);
        if (Y.HasValue) Y = Math.Clamp(Y.Value, Limits.MinCoordinate, Limits.MaxCoordinate);
        if (X.HasValue != Y.HasValue) { X = null; Y = null; }
        Margin = Math.Clamp(Margin, 0, Limits.MaxMargin);
        Width = Math.Clamp(Width, Limits.MinWidth, Limits.MaxWidth);
        Height = Math.Clamp(Height, Limits.MinHeight, Limits.MaxHeight);
        DesktopMode = Array.IndexOf(DesktopModes.All, DesktopMode) >= 0 ? DesktopMode : DesktopModes.Desktop;
        CountdownName = (CountdownName ?? "").Trim();
        if (CountdownName.Length > 60) CountdownName = CountdownName.Substring(0, 60);
        if (CountdownDate.HasValue)
        {
            CountdownDate = CountdownDate.Value.Date;
            if (CountdownDate.Value.Year < 1900 || CountdownDate.Value.Year > 2200) CountdownDate = null;
        }
    }
}
