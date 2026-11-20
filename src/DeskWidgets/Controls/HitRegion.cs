using System.Drawing;

namespace DeskWidgets.Controls;

internal enum HitAction
{
    PrevMonth,
    NextMonth,
    GoToday,
    SelectDay,
    OpenSettings,
    EditCountdown,
    DeleteCountdown
}

/// <summary>A clickable rectangle (in 96-DPI logical units) produced while drawing the dashboard.</summary>
internal sealed record HitRegion(RectangleF Rect, HitAction Action, DateTime? Date = null);
