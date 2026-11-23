using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Application.Games;

namespace DotnetTCG.Application.Tests;

internal sealed class FakeGameRepository : IGameRepository
{
    private readonly Dictionary<Guid, GameData> _games = [];

    public bool ThrowConflictOnce { get; set; }

    /// <summary>Cantidad de conflictos seguidos a simular (para el último reintento).</summary>
    public int ConflictCount { get; set; }

    public Task<GameData?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_games.GetValueOrDefault(id));

    public Task<IReadOnlyList<GameData>> ListLobbyAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<GameData>>(
            _games.Values.Where(g => g.Status == Domain.Enums.GameStatus.InLobby).ToList());

    public Task<IReadOnlyList<GameData>> ListResumableByUserAsync(Guid userId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<GameData>>([]);

    public Task<GameData?> FindActiveByUserAsync(Guid userId, CancellationToken cancellationToken)
        => Task.FromResult(_games.Values.FirstOrDefault(g =>
            g.Players.Any(p => p.UserId == userId)
            && g.Status != Domain.Enums.GameStatus.Finished
            && g.Status != Domain.Enums.GameStatus.Cancelled));

    public Task AddAsync(GameData game, CancellationToken cancellationToken)
    {
        _games[game.Id] = game;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(GameData game, uint expectedVersion, CancellationToken cancellationToken)
    {
        if (ThrowConflictOnce)
        {
            ThrowConflictOnce = false;
            throw new ConcurrencyConflictException();
        }

        if (ConflictCount > 0)
        {
            ConflictCount--;
            throw new ConcurrencyConflictException();
        }

        var current = _games[game.Id];
        if (current.Version != expectedVersion)
            throw new ConcurrencyConflictException();
        _games[game.Id] = game with { Version = current.Version + 1 };
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        _games.Remove(id);
        return Task.CompletedTask;
    }
}

internal sealed class FakeGameLogRepository : IGameLogRepository
{
    public List<(Guid GameId, string Action, string Result)> Entries { get; } = [];

    public Task AppendAsync(Guid gameId, int turnNumber, Guid? actingUserId,
        string action, string result, CancellationToken cancellationToken)
    {
        Entries.Add((gameId, action, result));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<GameLogEntry>> ListByGameAsync(Guid gameId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<GameLogEntry>>([]);
}

internal sealed class AlwaysHeadsCoinFlip : Domain.Game.ICoinFlipService
{
    private int _remaining = 10000;
    public bool FlipHeads() => _remaining-- > 0;
}
