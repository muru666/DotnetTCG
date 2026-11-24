using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Application.Games;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Infrastructure.Persistence;
using DotnetTCG.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DotnetTCG.Infrastructure.Persistence.Repositories;

public sealed class EfGameRepository(AppDbContext db) : IGameRepository
{
    public async Task<GameData?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.Games.AsNoTracking()
            .Include(g => g.Players).ThenInclude(p => p.User)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        return entity is null ? null : ToData(entity);
    }

    public async Task<IReadOnlyList<GameData>> ListLobbyAsync(CancellationToken cancellationToken)
    {
        var entities = await db.Games.AsNoTracking()
            .Include(g => g.Players).ThenInclude(p => p.User)
            .Where(g => g.Status == (int)GameStatus.InLobby)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(cancellationToken);
        return entities.Select(ToData).ToList();
    }

    public async Task<IReadOnlyList<GameData>> ListResumableByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var resumable = new[] { (int)GameStatus.Setup, (int)GameStatus.InProgress, (int)GameStatus.Paused };
        var entities = await db.Games.AsNoTracking()
            .Include(g => g.Players).ThenInclude(p => p.User)
            .Where(g => resumable.Contains(g.Status) && g.Players.Any(p => p.UserId == userId))
            .OrderByDescending(g => g.UpdatedAt)
            .ToListAsync(cancellationToken);
        return entities.Select(ToData).ToList();
    }

    public async Task<GameData?> FindActiveByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var active = new[] { (int)GameStatus.InLobby, (int)GameStatus.Setup, (int)GameStatus.InProgress, (int)GameStatus.Paused };
        var entity = await db.Games.AsNoTracking()
            .Include(g => g.Players).ThenInclude(p => p.User)
            .Where(g => active.Contains(g.Status) && g.Players.Any(p => p.UserId == userId))
            .OrderByDescending(g => g.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : ToData(entity);
    }

    public async Task AddAsync(GameData game, CancellationToken cancellationToken)
    {
        db.Games.Add(ToEntity(game));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(GameData game, uint expectedVersion, CancellationToken cancellationToken)
    {
        // En prod no hay transacción externa: la abrimos y somos dueños del commit.
        // En tests con transacción externa participamos en la existente.
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var tx = ownsTransaction
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var tracked = db.Games.Local.FirstOrDefault(g => g.Id == game.Id);
        if (tracked is not null)
            db.Entry(tracked).State = EntityState.Detached;

        var scalars = ToEntity(game);
        scalars.Players = [];
        db.Games.Attach(scalars);
        db.Entry(scalars).OriginalValues[nameof(GameEntity.Version)] = expectedVersion;
        db.Entry(scalars).State = EntityState.Modified;
        // ToEntity pone CreatedAt = UpdatedAt para el alta; en updates eso pisaría
        // la fecha real de creación (AUDIT-2026-11 BUG-005).
        db.Entry(scalars).Property(g => g.CreatedAt).IsModified = false;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException();
        }

        var current = await db.GamePlayers
            .Where(p => p.GameId == game.Id)
            .ToListAsync(cancellationToken);
        var desired = game.Players.ToDictionary(p => p.UserId);
        foreach (var existing in current.Where(p => !desired.ContainsKey(p.UserId)))
            db.GamePlayers.Remove(existing);
        foreach (var player in game.Players)
        {
            var existing = current.FirstOrDefault(p => p.UserId == player.UserId);
            if (existing is null)
            {
                db.GamePlayers.Add(new GamePlayerEntity
                {
                    GameId = game.Id,
                    UserId = player.UserId,
                    PlayerOrder = player.Order,
                    DeckId = player.DeckId,
                    IsReady = player.IsReady,
                });
            }
            else
            {
                existing.PlayerOrder = player.Order;
                existing.DeckId = player.DeckId;
                existing.IsReady = player.IsReady;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        if (ownsTransaction)
            await tx!.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await db.Games.Where(g => g.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    private static GameData ToData(GameEntity entity) => new(
        entity.Id,
        (GameStatus)entity.Status,
        entity.CurrentPlayerIndex,
        entity.TurnNumber,
        (TurnPhase)entity.TurnPhase,
        entity.StartingPlayerIndex,
        entity.SuddenDeath,
        entity.WinnerId,
        entity.CreatorId,
        entity.IsPublic,
        entity.AcceptsSpectators,
        entity.PasswordHash,
        entity.StateSnapshot,
        entity.Version,
        entity.Players.OrderBy(p => p.PlayerOrder)
            .Select(p => new GamePlayerData(p.UserId, p.PlayerOrder, p.DeckId, p.IsReady, p.User?.Username ?? "?")).ToList(),
        entity.UpdatedAt,
        entity.PreviousStatus,
        entity.PausedByUserId,
        entity.PausedAt);

    private static GameEntity ToEntity(GameData data) => new()
    {
        Id = data.Id,
        Status = (int)data.Status,
        CurrentPlayerIndex = data.CurrentPlayerIndex,
        TurnNumber = data.TurnNumber,
        TurnPhase = (int)data.Phase,
        StartingPlayerIndex = data.StartingPlayerIndex,
        SuddenDeath = data.SuddenDeath,
        WinnerId = data.WinnerId,
        CreatorId = data.CreatorId,
        IsPublic = data.IsPublic,
        AcceptsSpectators = data.AcceptsSpectators,
        PasswordHash = data.PasswordHash,
        StateSnapshot = data.Snapshot,
        Version = data.Version,
        CreatedAt = data.UpdatedAt,
        UpdatedAt = data.UpdatedAt,
        FinishedAt = data.Status == GameStatus.Finished ? data.UpdatedAt : null,
        PreviousStatus = data.PreviousStatus,
        PausedByUserId = data.PausedByUserId,
        PausedAt = data.PausedAt,
        Players = data.Players.Select(p => new GamePlayerEntity
        {
            GameId = data.Id,
            UserId = p.UserId,
            PlayerOrder = p.Order,
            DeckId = p.DeckId,
            IsReady = p.IsReady,
        }).ToList(),
    };
}

public sealed class EfGameLogRepository(AppDbContext db) : IGameLogRepository
{
    public async Task AppendAsync(
        Guid gameId, int turnNumber, Guid? actingUserId, string action, string result,
        CancellationToken cancellationToken)
    {
        db.GameLogs.Add(new GameLogEntity
        {
            GameId = gameId,
            TurnNumber = turnNumber,
            ActingUserId = actingUserId,
            Action = action,
            Result = result,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GameLogEntry>> ListByGameAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var entities = await db.GameLogs.AsNoTracking()
            .Where(l => l.GameId == gameId)
            .OrderBy(l => l.Id)
            .ToListAsync(cancellationToken);
        return entities
            .Select(l => new GameLogEntry(l.Id, l.GameId, l.TurnNumber, l.ActingUserId, l.Action, l.Result, l.CreatedAt))
            .ToList();
    }
}
