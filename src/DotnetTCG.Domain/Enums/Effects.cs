namespace DotnetTCG.Domain.Enums;

public enum Status
{
    Burned,
    Poisoned,
    Asleep,
    Paralyzed,
    Confused,
}

public enum EffectType
{
    ApplyStatus,
    Heal,
    SelfDamage,
    Draw,
    DiscardEnergy,
    ShuffleHand,
    DiscardOpponentHand,
    DiscardHand,
    SearchEnergy,
    HealAll,
    SwitchActive,
    SearchPokemon,
    Revive,
    DiscardDeck,
    PutCounters,
    DrawUntil,
    MoveEnergy,
    OpponentSwitch,
    DiscardAll,
    RemoveStatus,
    DrawShuffle,
    DiscardOpponentDeck,
    HealBench,
    PreventRetreat,
    RestrictAttack,
    RestrictItems,
    FlipStatus,
    FlipEnergy,
    Prevent,
    SearchAny,
    Recycle,
    FlipCoin,
}

public enum EffectTarget
{
    OwnActive,
    OwnBench,
    AllOwn,
    OpponentActive,
    OpponentBench,
    AllOpponents,
    SelfPlayer,
    OpponentPlayer,
}

public enum EffectTiming
{
    Immediate,
    BeforeDamage,
    AfterDamage,
}

public enum AbilityType
{
    Activated,
    Triggered,
    Passive,
    AncientTrait,
}

public enum ModifierType
{
    DamageBoost,
    DamageReduction,
    PreventDamage,
    CannotAttack,
    CannotRetreat,
    RestrictAttack,
    RestrictItems,
    BlockSupporters,
}

public enum ModifierDuration
{
    OneUse,
    UntilNextTurn,
    UntilEndOfTurn,
    Permanent,
}
