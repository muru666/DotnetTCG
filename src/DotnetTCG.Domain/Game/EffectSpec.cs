using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Domain.Game;

public sealed record EffectSpec(
    EffectType Type,
    EffectTarget Target = EffectTarget.OpponentActive,
    int Amount = 0,
    Status? Status = null,
    PokemonType? EnergyType = null,
    string? CardName = null,
    bool FlipToActivate = false);

public sealed record EffectContext(
    GameState State,
    int ActingPlayerIndex,
    PokemonInPlay? Source = null,
    PokemonInPlay? SelectedTarget = null);
