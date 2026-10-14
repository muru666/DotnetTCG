using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Application.Games;

public sealed record GamePlayerData(Guid UserId, int Order, Guid? DeckId, bool IsReady, string Username = "");

public sealed record GameData(
    Guid Id,
    GameStatus Status,
    int CurrentPlayerIndex,
    int TurnNumber,
    TurnPhase Phase,
    int StartingPlayerIndex,
    bool SuddenDeath,
    Guid? WinnerId,
    Guid CreatorId,
    bool IsPublic,
    bool AcceptsSpectators,
    string? PasswordHash,
    string? Snapshot,
    uint Version,
    List<GamePlayerData> Players,
    DateTimeOffset UpdatedAt,
    int? PreviousStatus = null,
    Guid? PausedByUserId = null,
    DateTimeOffset? PausedAt = null);
