using DotnetTCG.Domain.Abilities;
using DotnetTCG.Domain.Attacks;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Engine;

public sealed record EngineServices(
    RuleValidator Rules,
    TurnManager Turns,
    DamageCalculator Damage,
    StatusEffectManager Statuses,
    VictoryConditionChecker Victories,
    SetupService Setup,
    AttackRegistry Attacks,
    AbilityRegistry Abilities,
    EffectEngine Effects,
    AttackPipeline Pipeline,
    ICoinFlipService CoinFlip,
    Random Random);
