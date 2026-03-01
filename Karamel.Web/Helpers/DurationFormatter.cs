namespace Karamel.Web.Helpers;

public static class DurationFormatter
{
    /// <summary>Returns formatted duration string or null if duration is zero or negative.</summary>
    /// <param name="seconds">Duration in whole seconds.</param>
    /// <returns>"m:ss" for under 1 hour, "h:mm:ss" for 1 hour or more, null for zero/negative.</returns>
    public static string? Format(int seconds)
    {
        if (seconds <= 0) return null;
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? ts.ToString(@"h\:mm\:ss")
            : ts.ToString(@"m\:ss");
    }
}
