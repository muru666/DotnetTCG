using DotnetTCG.Application.Common.Interfaces;

namespace DotnetTCG.Infrastructure.Clock;

public sealed class SystemClock(TimeProvider time) : IClock
{
    // Un solo reloj de origen (TimeProvider): el mismo que usa el hub para la
    // gracia de desconexión, así un FakeTimeProvider mueve ambos (ARCH-003).
    public DateTimeOffset UtcNow => time.GetUtcNow();
}
