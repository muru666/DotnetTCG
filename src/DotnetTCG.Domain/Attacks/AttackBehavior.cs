using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Attacks;

public sealed record VariableDamageContext(
    GameState State,
    int ActingPlayerIndex,
    PokemonInPlay Attacker,
    PokemonInPlay Defender,
    int BaseDamage,
    ICoinFlipService CoinFlip,
    Random Random,
    bool OptionalDiscardAccepted,
    IReadOnlyList<EnergyCard> EnergiesToDiscard);

public sealed record ComplexContext(
    GameState State,
    int ActingPlayerIndex,
    PokemonInPlay Attacker,
    PokemonInPlay Defender,
    PokemonInPlay? SelectedTarget,
    EffectEngine Effects,
    StatusEffectManager Statuses,
    ICoinFlipService CoinFlip,
    Random Random);

public sealed record AttackBehavior(
    Func<VariableDamageContext, int>? VariableDamage = null,
    bool FlipToFail = false,
    IReadOnlyList<EffectSpec>? PostEffects = null,
    Action<ComplexContext>? Complex = null,
    EffectTarget? NeedsBenchTarget = null,
    bool NeedsOptionalDiscard = false,
    IReadOnlyList<string>? Aliases = null);
