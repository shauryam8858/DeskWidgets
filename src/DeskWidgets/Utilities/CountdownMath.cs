namespace DeskWidgets;

public readonly record struct CountdownResult(bool IsSet, bool IsComplete, int Days, int Hours, int Minutes);

public static class CountdownMath
{
    /// <summary>
    /// Time left until the start (00:00) of the target date. Days, hours and minutes are the
    /// whole units remaining, so 1 day 2 hours 3 minutes reads exactly like that.
    /// </summary>
    public static CountdownResult Compute(DateTime now, DateTime? targetDate)
    {
        if (targetDate is null) return new CountdownResult(false, false, 0, 0, 0);
        var remaining = targetDate.Value.Date - now;
        if (remaining <= TimeSpan.Zero) return new CountdownResult(true, true, 0, 0, 0);
        return new CountdownResult(true, false, (int)remaining.TotalDays, remaining.Hours, remaining.Minutes);
    }
}
