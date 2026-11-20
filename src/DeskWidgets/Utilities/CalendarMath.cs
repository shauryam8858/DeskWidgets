using System.Globalization;

namespace DeskWidgets;

public static class CalendarMath
{
    public const int MinYear = 1900;
    public const int MaxYear = 2200;

    /// <summary>The 42 dates (6 weeks) shown for a month, starting on Sunday or Monday.</summary>
    public static DateTime[] BuildGrid(int year, int month, bool mondayFirst)
    {
        year = Math.Clamp(year, MinYear, MaxYear);
        month = Math.Clamp(month, 1, 12);
        var first = new DateTime(year, month, 1);
        int dow = (int)first.DayOfWeek; // Sunday = 0
        int offset = mondayFirst ? (dow + 6) % 7 : dow;
        var start = first.AddDays(-offset);
        var grid = new DateTime[42];
        for (int i = 0; i < 42; i++) grid[i] = start.AddDays(i);
        return grid;
    }

    /// <summary>Moves by whole months and keeps the result inside the supported year range.</summary>
    public static (int Year, int Month) AddMonths(int year, int month, int delta)
    {
        int index = year * 12 + (month - 1) + delta;
        int y = index / 12;
        int m = index % 12 + 1;
        if (y < MinYear) return (MinYear, 1);
        if (y > MaxYear) return (MaxYear, 12);
        return (y, m);
    }

    public static int DaysInMonth(int year, int month) => DateTime.DaysInMonth(year, month);

    public static bool IsLeapYear(int year) => DateTime.IsLeapYear(year);

    /// <summary>Short weekday names in display order, e.g. "Su","Mo",...</summary>
    public static string[] WeekdayLabels(bool mondayFirst)
    {
        var names = CultureInfo.CurrentCulture.DateTimeFormat.ShortestDayNames; // index 0 = Sunday
        var result = new string[7];
        for (int i = 0; i < 7; i++)
        {
            int dow = mondayFirst ? (i + 1) % 7 : i;
            result[i] = names[dow];
        }
        return result;
    }
}
