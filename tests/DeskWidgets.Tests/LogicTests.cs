using System.Drawing;
using DeskWidgets.Models;
using Xunit;

namespace DeskWidgets.Tests;

public class CalendarMathTests
{
    [Theory]
    [InlineData(2024, true)]
    [InlineData(2000, true)]
    [InlineData(2100, false)]
    [InlineData(2026, false)]
    public void LeapYears(int year, bool expected) => Assert.Equal(expected, CalendarMath.IsLeapYear(year));

    [Fact]
    public void FebruaryLength()
    {
        Assert.Equal(29, CalendarMath.DaysInMonth(2024, 2));
        Assert.Equal(28, CalendarMath.DaysInMonth(2026, 2));
    }

    [Fact]
    public void GridHas42ConsecutiveDays()
    {
        var grid = CalendarMath.BuildGrid(2026, 11, false);
        Assert.Equal(42, grid.Length);
        for (int i = 1; i < 42; i++) Assert.Equal(grid[i - 1].AddDays(1), grid[i]);
    }

    [Fact]
    public void November2026StartsOnSunday()
    {
        // 1 November 2026 is a Sunday.
        Assert.Equal(new DateTime(2026, 11, 1), CalendarMath.BuildGrid(2026, 11, false)[0]);
        Assert.Equal(new DateTime(2026, 10, 26), CalendarMath.BuildGrid(2026, 11, true)[0]); // Monday-first
    }

    [Fact]
    public void February2024StartsOnThursday()
    {
        var sunFirst = CalendarMath.BuildGrid(2024, 2, false);
        var monFirst = CalendarMath.BuildGrid(2024, 2, true);
        Assert.Equal(new DateTime(2024, 2, 1), sunFirst[4]);
        Assert.Equal(new DateTime(2024, 2, 1), monFirst[3]);
        Assert.Contains(new DateTime(2024, 2, 29), sunFirst);
    }

    [Fact]
    public void AddMonthsWrapsYearsAndClamps()
    {
        Assert.Equal((2027, 1), CalendarMath.AddMonths(2026, 12, 1));
        Assert.Equal((2025, 12), CalendarMath.AddMonths(2026, 1, -1));
        Assert.Equal((CalendarMath.MinYear, 1), CalendarMath.AddMonths(CalendarMath.MinYear, 1, -1));
        Assert.Equal((CalendarMath.MaxYear, 12), CalendarMath.AddMonths(CalendarMath.MaxYear, 12, 1));
    }

    [Fact]
    public void WeekdayLabelsHaveSevenEntries()
    {
        Assert.Equal(7, CalendarMath.WeekdayLabels(true).Length);
        Assert.Equal(7, CalendarMath.WeekdayLabels(false).Length);
    }
}

public class CountdownMathTests
{
    [Fact]
    public void NoTargetMeansNotSet()
    {
        var r = CountdownMath.Compute(new DateTime(2026, 11, 20, 8, 44, 0), null);
        Assert.False(r.IsSet);
        Assert.False(r.IsComplete);
    }

    [Fact]
    public void RemainingTimeIsSplitIntoDaysHoursMinutes()
    {
        var r = CountdownMath.Compute(new DateTime(2026, 11, 20, 8, 44, 0), new DateTime(2026, 11, 22));
        Assert.True(r.IsSet);
        Assert.False(r.IsComplete);
        Assert.Equal(1, r.Days);
        Assert.Equal(15, r.Hours);
        Assert.Equal(16, r.Minutes);
    }

    [Fact]
    public void ReachingTheTargetCompletesTheCountdown()
    {
        Assert.True(CountdownMath.Compute(new DateTime(2026, 11, 22, 0, 0, 0), new DateTime(2026, 11, 22)).IsComplete);
        Assert.True(CountdownMath.Compute(new DateTime(2026, 11, 23, 10, 0, 0), new DateTime(2026, 11, 22)).IsComplete);
    }

    [Fact]
    public void TimeOfDayInTargetIsIgnored()
    {
        var a = CountdownMath.Compute(new DateTime(2026, 1, 1), new DateTime(2026, 1, 11, 18, 30, 0));
        Assert.Equal(10, a.Days);
        Assert.Equal(0, a.Hours);
    }

    [Fact]
    public void CountdownAcrossLeapDay()
    {
        var r = CountdownMath.Compute(new DateTime(2028, 2, 28), new DateTime(2028, 3, 1));
        Assert.Equal(2, r.Days);
    }
}

public class SettingsTests
{
    [Fact]
    public void DefaultsMatchTheSpecification()
    {
        var s = new AppSettings();
        Assert.Equal(ThemeNames.DarkGlass, s.Theme);
        Assert.Equal(85, s.OpacityPercent);
        Assert.Equal(20, s.CornerRadius);
        Assert.Equal(PositionPresets.MiddleRight, s.PositionPreset);
        Assert.False(s.StartWithWindows);
        Assert.Null(s.CountdownDate); // no hard-coded exam date
    }

    [Fact]
    public void RoundTripKeepsEveryValue()
    {
        var s = new AppSettings
        {
            Theme = ThemeNames.MidnightBlue, OpacityPercent = 42, CornerRadius = 12, Shadow = false,
            AccentColor = "#112233", UseCustomAccent = true, BackgroundColor = "#445566", UseCustomBackground = true,
            PositionPreset = PositionPresets.Custom, X = 120, Y = -30, Margin = 8, LockPosition = true,
            Width = 420, Height = 600, Visible = false, DesktopMode = DesktopModes.Normal,
            Use24Hour = true, ShowSeconds = false, ShowDate = false, WeekStartsOnMonday = true,
            CountdownName = "NEET 2027", CountdownDate = new DateTime(2027, 5, 3), StartWithWindows = true
        };
        var copy = SettingsSerializer.TryDeserialize(SettingsSerializer.Serialize(s));
        Assert.NotNull(copy);
        Assert.Equal(SettingsSerializer.Serialize(s), SettingsSerializer.Serialize(copy!));
        Assert.Equal(new DateTime(2027, 5, 3), copy!.CountdownDate);
        Assert.Equal(-30, copy.Y);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not json")]
    [InlineData("[1,2,3]")]
    [InlineData("null")]
    public void DamagedFilesReturnNull(string text) => Assert.Null(SettingsSerializer.TryDeserialize(text));

    [Fact]
    public void MissingPropertiesFallBackToDefaults()
    {
        var s = SettingsSerializer.TryDeserialize("{}");
        Assert.NotNull(s);
        Assert.Equal(85, s!.OpacityPercent);
    }

    [Fact]
    public void InvalidValuesAreRepaired()
    {
        var s = SettingsSerializer.TryDeserialize(
            "{\"OpacityPercent\":500,\"Width\":5,\"Height\":99999,\"Theme\":\"Neon\",\"AccentColor\":\"red\",\"PositionPreset\":\"Moon\",\"X\":10,\"CornerRadius\":-4}");
        Assert.NotNull(s);
        Assert.Equal(100, s!.OpacityPercent);
        Assert.Equal(Limits.MinWidth, s.Width);
        Assert.Equal(Limits.MaxHeight, s.Height);
        Assert.Equal(ThemeNames.DarkGlass, s.Theme);
        Assert.Equal("#6C8EBF", s.AccentColor);
        Assert.Equal(PositionPresets.MiddleRight, s.PositionPreset);
        Assert.Null(s.X); // X without Y is dropped
        Assert.Equal(0, s.CornerRadius);
    }

    [Fact]
    public void ZeroOpacityIsAllowed()
    {
        var s = SettingsSerializer.TryDeserialize("{\"OpacityPercent\":0}");
        Assert.Equal(0, s!.OpacityPercent);
    }

    [Fact]
    public void HexColorParsing()
    {
        Assert.True(HexColor.TryParse("#6C8EBF", out var c));
        Assert.Equal(Color.FromArgb(255, 0x6C, 0x8E, 0xBF), c);
        Assert.Equal("#6C8EBF", HexColor.ToHex(c));
        Assert.False(HexColor.TryParse("6C8E", out _));
        Assert.False(HexColor.TryParse("#GGGGGG", out _));
    }
}

public class PositionMathTests
{
    private static readonly Rectangle Area = new(0, 0, 1366, 728);
    private static readonly Size Window = new(388, 588); // 360x560 panel + 14px inset on each side

    [Fact]
    public void MiddleRightIsTheDefaultAnchor()
    {
        var p = PositionMath.PresetPosition(PositionPresets.MiddleRight, Area, Window, 24, 14);
        Assert.Equal(1366 - 24 - 360 - 14, p.X);
        Assert.Equal((728 - 560) / 2 - 14, p.Y);
    }

    [Fact]
    public void CornerPresetsRespectTheMargin()
    {
        var tl = PositionMath.PresetPosition(PositionPresets.TopLeft, Area, Window, 24, 14);
        Assert.Equal(new Point(10, 10), tl);
        var br = PositionMath.PresetPosition(PositionPresets.BottomRight, Area, Window, 24, 14);
        Assert.Equal(1366 - 24 - 360 - 14, br.X);
        Assert.Equal(728 - 24 - 560 - 14, br.Y);
    }

    [Fact]
    public void OffScreenWindowIsRecoveredOntoThePrimaryScreen()
    {
        var lost = new Rectangle(-5000, -5000, 388, 588);
        var fixedRect = PositionMath.RecoverIfOffScreen(lost, new[] { Area }, Area);
        Assert.NotNull(fixedRect);
        Assert.True(Area.Contains(fixedRect!.Value));
    }

    [Fact]
    public void VisibleWindowIsLeftAlone()
        => Assert.Null(PositionMath.RecoverIfOffScreen(new Rectangle(900, 50, 388, 588), new[] { Area }, Area));

    [Fact]
    public void WindowOnlyBarelyOnScreenIsRecovered()
        => Assert.NotNull(PositionMath.RecoverIfOffScreen(new Rectangle(1360, 50, 388, 588), new[] { Area }, Area));

    [Fact]
    public void PanelSizeIsClampedToTheScreenAndLimits()
    {
        var size = PositionMath.ClampPanelSize(5000, 5000, Area, 14);
        Assert.True(size.Width <= Area.Width - 28);
        Assert.True(size.Height <= Area.Height - 28);
        var small = PositionMath.ClampPanelSize(10, 10, Area, 14);
        Assert.Equal(new Size(Limits.MinWidth, Limits.MinHeight), small);
    }
}
