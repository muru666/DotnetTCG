using System.Collections.Concurrent;

namespace DotnetTCG.Api.Hubs;

public sealed record TrackedConnection(Guid UserId, Guid GameId, bool Spectator);

public sealed class GameConnectionTracker
{
    private readonly ConcurrentDictionary<string, TrackedConnection> _connections = new();

    public void Add(string connectionId, Guid userId, Guid gameId, bool spectator)
        => _connections[connectionId] = new TrackedConnection(userId, gameId, spectator);

    public TrackedConnection? Remove(string connectionId)
        => _connections.TryRemove(connectionId, out var tracked) ? tracked : null;

    public IReadOnlyList<string> ConnectionIdsOf(Guid userId, Guid gameId)
        => _connections
            .Where(kv => kv.Value.UserId == userId && kv.Value.GameId == gameId)
            .Select(kv => kv.Key)
            .ToList();

    public IReadOnlyList<Guid> SpectatorsOf(Guid gameId)
        => _connections.Values
            .Where(c => c.GameId == gameId && c.Spectator)
            .Select(c => c.UserId)
            .Distinct()
            .ToList();

    public bool HasConnection(Guid userId, Guid gameId)
        => _connections.Values.Any(c => c.UserId == userId && c.GameId == gameId);

    public bool IsConnected(Guid userId)
        => _connections.Values.Any(c => c.UserId == userId);
}
