using DotnetTCG.Infrastructure.Clock;

namespace DotnetTCG.Infrastructure.Tests;

public sealed class SystemClockTests
{
    [SkippableFact]
    public void UtcNow_IsCloseToUtcNow()
    {
        var clock = new SystemClock(TimeProvider.System);

        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        var now = clock.UtcNow;
        var after = DateTimeOffset.UtcNow.AddSeconds(5);

        Assert.InRange(now, before, after);
        Assert.Equal(TimeSpan.Zero, now.Offset);
    }
}
