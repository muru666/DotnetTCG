using DotnetTCG.Application.Common.Interfaces;

namespace DotnetTCG.Infrastructure.Clock;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
