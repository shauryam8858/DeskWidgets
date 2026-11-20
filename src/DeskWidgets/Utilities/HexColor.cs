using System.Drawing;
using System.Globalization;

namespace DeskWidgets;

/// <summary>Converts colours to and from "#RRGGBB" text without needing System.Drawing.Common.</summary>
public static class HexColor
{
    public static bool TryParse(string? text, out Color color)
    {
        color = Color.Empty;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim();
        if (t.StartsWith('#')) t = t.Substring(1);
        if (t.Length != 6) return false;
        if (!int.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb)) return false;
        color = Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        return true;
    }

    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static Color ParseOr(string? text, Color fallback) => TryParse(text, out var c) ? c : fallback;

    /// <summary>Perceived brightness from 0 (black) to 1 (white).</summary>
    public static double Luminance(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
}
