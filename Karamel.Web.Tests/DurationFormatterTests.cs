using Karamel.Web.Helpers;

namespace Karamel.Web.Tests;

public class DurationFormatterTests
{
    [Fact]
    public void Format_ZeroDuration_ReturnsNull()
    {
        Assert.Null(DurationFormatter.Format(0));
    }

    [Fact]
    public void Format_NegativeDuration_ReturnsNull()
    {
        Assert.Null(DurationFormatter.Format(-1));
        Assert.Null(DurationFormatter.Format(-100));
    }

    [Fact]
    public void Format_215Seconds_ReturnsMinuteColonSS()
    {
        // 215 seconds = 3 minutes 35 seconds
        Assert.Equal("3:35", DurationFormatter.Format(215));
    }

    [Fact]
    public void Format_3661Seconds_ReturnsHourColonMMColonSS()
    {
        // 3661 seconds = 1 hour 1 minute 1 second
        Assert.Equal("1:01:01", DurationFormatter.Format(3661));
    }

    [Fact]
    public void Format_59Seconds_ReturnsSingleMinute()
    {
        Assert.Equal("0:59", DurationFormatter.Format(59));
    }

    [Fact]
    public void Format_60Seconds_ReturnsOneMinuteZeroSeconds()
    {
        Assert.Equal("1:00", DurationFormatter.Format(60));
    }

    [Fact]
    public void Format_3599Seconds_ReturnsLastSubHourValue()
    {
        // 3599 = 59 min 59 sec — still under 1 hour, uses m:ss format
        Assert.Equal("59:59", DurationFormatter.Format(3599));
    }

    [Fact]
    public void Format_3600Seconds_ReturnsHourFormat()
    {
        // 3600 = exactly 1 hour, switches to h:mm:ss
        Assert.Equal("1:00:00", DurationFormatter.Format(3600));
    }

    [Fact]
    public void Format_1Second_ReturnsZeroColonZeroOne()
    {
        Assert.Equal("0:01", DurationFormatter.Format(1));
    }
}
