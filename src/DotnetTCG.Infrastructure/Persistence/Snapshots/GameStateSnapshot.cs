using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Infrastructure.Persistence.Snapshots;

internal sealed record ModifierSnapshot(
    ModifierType Type,
    string Source,
    int Value,
    ModifierDuration Duration,
    int TurnApplied);

internal sealed record InPlaySnapshot(
    string CardId,
    int TotalDamage,
    List<string> Energies,
    string? ToolId,
    List<Status> Statuses,
    List<ModifierSnapshot> Modifiers,
    bool JustPlayed,
    int CannotAttackUntilTurn);

internal sealed record BoardSnapshot(
    InPlaySnapshot? Active,
    List<InPlaySnapshot> Bench,
    List<string> Hand,
    List<string> Deck,
    List<string> Discard,
    List<string> Prizes,
    List<ModifierSnapshot> Modifiers);

internal sealed record GameStateSnapshot(
    Guid Id,
    GameStatus Status,
    TurnPhase Phase,
    int CurrentPlayerIndex,
    int TurnNumber,
    int StartingPlayerIndex,
    bool SuddenDeath,
    int[] MulliganCounts,
    bool[] SetupConfirmed,
    string? StadiumCardId,
    PendingChoice? PendingChoice,
    PendingAttack? PendingAttack,
    int? WinnerPlayerIndex,
    int EnergiesAttached,
    int SupportersPlayed,
    int StadiumsPlayed,
    int Retreats,
    int AbilitiesUsed,
    int[] SupportersBlocked,
    List<BoardSnapshot> Players);
