using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Application.Games;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Infrastructure.Persistence;
using DotnetTCG.Infrastructure.Persistence.Entities;
using DotnetTCG.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DotnetTCG.Infrastructure.Tests;

public sealed class EfGameRepositoryTests
{
    private static async Task<(AppDbContext Db, IDbContextTransaction Tx)?> OpenOrSkipAsync()
        // Postgres no disponible (sin Docker): el test se omite devolviendo null.
        => await TestPostgres.TryOpenAsync();

    private static async Task<UserEntity> NewUserAsync(AppDbContext db)
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Username = $"u_{Guid.NewGuid():N}"[..20],
            Email = $"{Guid.NewGuid():N}@x.test",
            PasswordHash = "hash",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static GameData NewLobby(Guid creatorId) => new(
        Guid.NewGuid(), GameStatus.InLobby, 0, 0, TurnPhase.Draw, 0, false, null,
        creatorId, IsPublic: true, AcceptsSpectators: true, PasswordHash: null,
        Snapshot: null, Version: 0,
        [new GamePlayerData(creatorId, 0, null, false)],
        DateTimeOffset.UtcNow);

    [Fact]
    public async Task Games_AddGetUpdateDelete_RoundTrip()
    {
        if (await OpenOrSkipAsync() is not (var db, var tx))
            return;
        await using var _ = db;
        await using var _tx = tx;
        var repo = new EfGameRepository(db);
        var creator = await NewUserAsync(db);
        var game = NewLobby(creator.Id);

        await repo.AddAsync(game, CancellationToken.None);
        var loaded = await repo.GetByIdAsync(game.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(GameStatus.InLobby, loaded!.Status);
        Assert.Single(loaded.Players);
        Assert.True(loaded.Version > 0);

        var lobby = await repo.ListLobbyAsync(CancellationToken.None);
        Assert.Contains(lobby, g => g.Id == game.Id);

        var updated = loaded with
        {
            Status = GameStatus.Setup,
            Snapshot = "{\"x\":1}",
            Players = [new GamePlayerData(creator.Id, 0, null, true)],
        };
        await repo.UpdateAsync(updated, loaded.Version, CancellationToken.None);

        var reloaded = await repo.GetByIdAsync(game.Id, CancellationToken.None);
        Assert.Equal(GameStatus.Setup, reloaded!.Status);
        // jsonb reformatea: comparar el valor, no el string exacto.
        using (var doc = System.Text.Json.JsonDocument.Parse(reloaded.Snapshot!))
            Assert.Equal(1, doc.RootElement.GetProperty("x").GetInt32());

        await repo.DeleteAsync(game.Id, CancellationToken.None);
        Assert.Null(await repo.GetByIdAsync(game.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Games_Update_WithStaleVersion_ThrowsConflict()
    {
        if (await OpenOrSkipAsync() is not (var db, var tx))
            return;
        await using var _ = db;
        await using var _tx = tx;
        var repo = new EfGameRepository(db);
        var creator = await NewUserAsync(db);
        var game = NewLobby(creator.Id);
        await repo.AddAsync(game, CancellationToken.None);
        var loaded = await repo.GetByIdAsync(game.Id, CancellationToken.None);

        await repo.UpdateAsync(loaded! with { Status = GameStatus.Setup },
            loaded.Version, CancellationToken.None);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            repo.UpdateAsync(loaded with { Status = GameStatus.Cancelled },
                loaded.Version, CancellationToken.None));
    }

    [Fact]
    public async Task Games_Update_SyncsPlayerSet()
    {
        if (await OpenOrSkipAsync() is not (var db, var tx))
            return;
        await using var _ = db;
        await using var _tx = tx;
        var repo = new EfGameRepository(db);
        var creator = await NewUserAsync(db);
        var joiner = await NewUserAsync(db);
        var game = NewLobby(creator.Id);
        await repo.AddAsync(game, CancellationToken.None);
        var loaded = await repo.GetByIdAsync(game.Id, CancellationToken.None);

        await repo.UpdateAsync(loaded! with
        {
            Players = [new GamePlayerData(joiner.Id, 1, null, false)],
            CreatorId = joiner.Id,
        }, loaded.Version, CancellationToken.None);

        var reloaded = await repo.GetByIdAsync(game.Id, CancellationToken.None);
        Assert.Single(reloaded!.Players);
        Assert.Equal(joiner.Id, reloaded.Players[0].UserId);
    }

    [Fact]
    public async Task Games_ResumableAndActive_FiltersByUser()
    {
        if (await OpenOrSkipAsync() is not (var db, var tx))
            return;
        await using var _ = db;
        await using var _tx = tx;
        var repo = new EfGameRepository(db);
        var user = await NewUserAsync(db);
        var other = await NewUserAsync(db);
        var game = NewLobby(user.Id);
        await repo.AddAsync(game, CancellationToken.None);
        var loaded = await repo.GetByIdAsync(game.Id, CancellationToken.None);
        await repo.UpdateAsync(loaded! with { Status = GameStatus.InProgress },
            loaded.Version, CancellationToken.None);

        var resumable = await repo.ListResumableByUserAsync(user.Id, CancellationToken.None);
        var resumableOther = await repo.ListResumableByUserAsync(other.Id, CancellationToken.None);
        var active = await repo.FindActiveByUserAsync(user.Id, CancellationToken.None);

        Assert.Contains(resumable, g => g.Id == game.Id);
        Assert.DoesNotContain(resumableOther, g => g.Id == game.Id);
        Assert.NotNull(active);
    }

    [Fact]
    public async Task GameLogs_AppendAndList_OrdersById()
    {
        if (await OpenOrSkipAsync() is not (var db, var tx))
            return;
        await using var _ = db;
        await using var _tx = tx;
        var repo = new EfGameLogRepository(db);
        var creator = await NewUserAsync(db);
        var gameRepo = new EfGameRepository(db);
        var game = NewLobby(creator.Id);
        await gameRepo.AddAsync(game, CancellationToken.None);

        await repo.AppendAsync(game.Id, 1, creator.Id, "GameStart", "Ok", CancellationToken.None);
        await repo.AppendAsync(game.Id, 1, creator.Id, "Attack", "Ok", CancellationToken.None);
        var logs = await repo.ListByGameAsync(game.Id, CancellationToken.None);

        Assert.Equal(2, logs.Count);
        Assert.Equal("GameStart", logs[0].Action);
        Assert.Equal("Attack", logs[1].Action);
    }

    [Fact]
    public async Task GameLogs_UnknownGame_ReturnsEmpty()
    {
        if (await OpenOrSkipAsync() is not (var db, var tx))
            return;
        await using var _ = db;
        await using var _tx = tx;
        var repo = new EfGameLogRepository(db);

        var logs = await repo.ListByGameAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(logs);
    }
}
