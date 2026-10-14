using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Application.Games;

public sealed record GameSummaryDto(
    Guid Id,
    GameStatus Status,
    int TurnNumber,
    TurnPhase Phase,
    int CurrentPlayerIndex,
    Guid? WinnerId,
    IReadOnlyList<LobbyPlayerDto> Players);
