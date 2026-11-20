using System.Drawing;
using DeskWidgets.Models;

namespace DeskWidgets;

/// <summary>
/// Pure geometry helpers. A "window" is the visible panel plus a transparent border of
/// <c>inset</c> pixels on every side (room for the soft shadow).
/// </summary>
public static class PositionMath
{
    /// <summary>Top-left of the WINDOW for a preset, so the visible panel sits <c>margin</c> from the edges.</summary>
    public static Point PresetPosition(string preset, Rectangle area, Size window, int margin, int inset)
    {
        int panelW = window.Width - 2 * inset;
        int panelH = window.Height - 2 * inset;

        int left = area.Left + margin;
        int centreX = area.Left + (area.Width - panelW) / 2;
        int right = area.Right - margin - panelW;
        int top = area.Top + margin;
        int centreY = area.Top + (area.Height - panelH) / 2;
        int bottom = area.Bottom - margin - panelH;

        int px, py;
        switch (preset)
        {
            case PositionPresets.TopLeft: px = left; py = top; break;
            case PositionPresets.TopCentre: px = centreX; py = top; break;
            case PositionPresets.TopRight: px = right; py = top; break;
            case PositionPresets.MiddleLeft: px = left; py = centreY; break;
            case PositionPresets.Centre: px = centreX; py = centreY; break;
            case PositionPresets.BottomLeft: px = left; py = bottom; break;
            case PositionPresets.BottomCentre: px = centreX; py = bottom; break;
            case PositionPresets.BottomRight: px = right; py = bottom; break;
            default: px = right; py = centreY; break; // MiddleRight (the default)
        }

        // Never let a large panel start outside the top-left of the work area.
        px = Math.Max(px, area.Left);
        py = Math.Max(py, area.Top);
        return new Point(px - inset, py - inset);
    }

    /// <summary>Largest panel size that fits the work area, and never below the minimum when it does fit.</summary>
    public static Size ClampPanelSize(int width, int height, Rectangle area, int inset)
    {
        int maxW = Math.Min(Limits.MaxWidth, Math.Max(100, area.Width - 2 * inset));
        int maxH = Math.Min(Limits.MaxHeight, Math.Max(100, area.Height - 2 * inset));
        int minW = Math.Min(Limits.MinWidth, maxW);
        int minH = Math.Min(Limits.MinHeight, maxH);
        return new Size(Math.Clamp(width, minW, maxW), Math.Clamp(height, minH, maxH));
    }

    /// <summary>
    /// Returns null when the window is already reachable on some screen. Otherwise returns a
    /// new rectangle (same size) placed on the primary work area.
    /// </summary>
    public static Rectangle? RecoverIfOffScreen(Rectangle window, IReadOnlyList<Rectangle> workAreas,
        Rectangle primaryArea, int minVisibleWidth = 120, int minVisibleHeight = 80)
    {
        foreach (var area in workAreas)
        {
            var overlap = Rectangle.Intersect(window, area);
            if (overlap.Width >= minVisibleWidth && overlap.Height >= minVisibleHeight) return null;
        }

        int x = primaryArea.Left + Math.Max(0, (primaryArea.Width - window.Width) / 2);
        int y = primaryArea.Top + Math.Max(0, (primaryArea.Height - window.Height) / 2);
        return new Rectangle(x, y, window.Width, window.Height);
    }
}
