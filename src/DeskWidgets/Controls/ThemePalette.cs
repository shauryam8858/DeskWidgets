using System.Drawing;
using DeskWidgets.Models;

namespace DeskWidgets.Controls;

/// <summary>The resolved colours for the current theme plus any custom background/accent override.</summary>
internal sealed record ThemePalette(Color Background, Color Text, Color TextDim, Color Accent, Color AccentText, bool IsLight)
{
    public static ThemePalette Resolve(AppSettings s)
    {
        Color bg, accent;
        switch (s.Theme)
        {
            case ThemeNames.LightGlass:
                bg = Color.FromArgb(244, 246, 250);
                accent = Color.FromArgb(74, 111, 165);
                break;
            case ThemeNames.MidnightBlue:
                bg = Color.FromArgb(15, 26, 51);
                accent = Color.FromArgb(94, 135, 201);
                break;
            default:
                bg = Color.FromArgb(30, 34, 41);
                accent = Color.FromArgb(108, 142, 191);
                break;
        }

        if (s.UseCustomBackground) bg = HexColor.ParseOr(s.BackgroundColor, bg);
        if (s.UseCustomAccent) accent = HexColor.ParseOr(s.AccentColor, accent);

        bool light = HexColor.Luminance(bg) > 0.6;
        Color text, dim;
        if (light)
        {
            text = Color.FromArgb(27, 35, 48);
            dim = Color.FromArgb(91, 102, 118);
        }
        else if (s.Theme == ThemeNames.MidnightBlue && !s.UseCustomBackground)
        {
            text = Color.FromArgb(230, 236, 250);
            dim = Color.FromArgb(140, 155, 192);
        }
        else
        {
            text = Color.FromArgb(242, 244, 248);
            dim = Color.FromArgb(154, 163, 178);
        }

        Color accentText = HexColor.Luminance(accent) > 0.6 ? Color.FromArgb(20, 24, 32) : Color.White;
        return new ThemePalette(bg, text, dim, accent, accentText, light);
    }
}
