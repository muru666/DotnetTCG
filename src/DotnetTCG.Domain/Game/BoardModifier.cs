using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Domain.Game;

public sealed record BoardModifier(
    ModifierType Type,
    string Source,
    int Value,
    ModifierDuration Duration,
    int TurnApplied);
