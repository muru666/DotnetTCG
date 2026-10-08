using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Engine;

public sealed record GameAction(
    GameActionType Type,
    int PlayerIndex,
    Card? Card = null,
    PokemonInPlay? Target = null,
    Attack? Attack = null,
    IReadOnlyList<EnergyCard>? EnergiesToDiscard = null,
    string? ChoiceId = null,
    IReadOnlyList<string>? SelectedOptionIds = null,
    string? AbilityName = null);

public sealed record GameActionResult(
    bool Success,
    string? Error,
    PendingChoice? PendingChoice = null,
    IReadOnlyList<GameEvent>? Events = null);
