using DotnetTCG.Application.Games;

namespace DotnetTCG.Application.Common.Interfaces;

public sealed class ConcurrencyConflictException : InvalidOperationException
{
    public ConcurrencyConflictException() : base("Game changed concurrently.") { }
}

public interface IGameRepository
{
    Task<GameData?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<GameData>> ListLobbyAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<GameData>> ListResumableByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<GameData?> FindActiveByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task AddAsync(GameData game, CancellationToken cancellationToken);
    Task UpdateAsync(GameData game, uint expectedVersion, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public interface IGameLogRepository
{
    Task AppendAsync(
        Guid gameId, int turnNumber, Guid? actingUserId, string action, string result,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<GameLogEntry>> ListByGameAsync(Guid gameId, CancellationToken cancellationToken);
}

public sealed record GameLogEntry(
    long Id, Guid GameId, int TurnNumber, Guid? ActingUserId, string Action, string Result, DateTimeOffset CreatedAt);
