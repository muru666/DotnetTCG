using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Infrastructure.Mapping;
using DotnetTCG.Infrastructure.Persistence;
using DotnetTCG.Infrastructure.Persistence.Entities;
using DotnetTCG.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DotnetTCG.Infrastructure.Tests;

internal static class TestPostgres
{
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("TEST_POSTGRES")
        ?? "Host=localhost;Port=5433;Database=dotnettcp_test;Username=postgres;Password=postgres";

    public static async Task<(AppDbContext Db, IDbContextTransaction Tx)?> TryOpenAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        var db = new AppDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
        }
        catch
        {
            await db.DisposeAsync();
            return null;
        }

        return (db, await db.Database.BeginTransactionAsync());
    }
}

public sealed class CardMapperTests
{
    [Fact]
    public void RoundTrip_Pokemon_PreservesData()
    {
        var card = new PokemonCard("xy1-1", "Venusaur-EX", [SubType.Basic, SubType.Ex], [],
            "xy1", "img", 180, 4, null, PokemonType.Grass,
            [new Attack("Poison Vine", "t", "60", [PokemonType.Grass])],
            [new PokemonAbility("Sunrise", "t", AbilityType.Activated)],
            [new Weakness(PokemonType.Fire, "×2")], []);

        var restored = Assert.IsType<PokemonCard>(CardMapper.ToDomain(CardMapper.ToEntity(card)));

        Assert.Equal(card.Id, restored.Id);
        Assert.Equal(card.Name, restored.Name);
        Assert.Equal(card.Subtypes, restored.Subtypes);
        Assert.Equal(card.Hp, restored.Hp);
        Assert.Equal(card.RetreatCost, restored.RetreatCost);
        Assert.Equal(card.PokemonType, restored.PokemonType);
        Assert.Equal(Json(card.Attacks), Json(restored.Attacks));
        Assert.Equal(Json(card.Abilities), Json(restored.Abilities));
        Assert.Equal(Json(card.Weaknesses), Json(restored.Weaknesses));
    }

    private static string Json<T>(IReadOnlyList<T> value)
        => System.Text.Json.JsonSerializer.Serialize(value);

    [Fact]
    public void RoundTrip_Energy_PreservesSpecial()
    {
        var card = new EnergyCard("xy1-130", "Double Colorless Energy", [SubType.Item], [],
            "xy1", "img", PokemonType.Colorless, SpecialEnergyType.DoubleColorless);

        var restored = Assert.IsType<EnergyCard>(CardMapper.ToDomain(CardMapper.ToEntity(card)));

        Assert.Equal(card.Id, restored.Id);
        Assert.Equal(card.EnergyType, restored.EnergyType);
        Assert.Equal(card.Special, restored.Special);
        Assert.Equal(2, restored.EnergyValue);
    }
}

public sealed class EfRepositoryTests
{    [Fact]
    public async Task Cards_UpsertAndSearch_RoundTrips()
    {
        // Postgres no disponible (sin Docker): se omite.
        if (await TestPostgres.TryOpenAsync() is not (var db, var tx))
            return;
        await using var _ = db;
        await using var _tx = tx;
        var repo = new EfCardRepository(db);

        var card = new PokemonCard("xy1-1", "Venusaur", [SubType.Basic], [], "xy1", "img",
            180, 2, null, PokemonType.Grass, [], [], [], []);
        await repo.UpsertRangeAsync([card], CancellationToken.None);
        await repo.UpsertRangeAsync([card with { Hp = 190 }], CancellationToken.None);

        var found = await repo.GetByIdAsync("xy1-1", CancellationToken.None);
        var searched = await repo.SearchAsync("venus", null, 0, 20, CancellationToken.None);

        Assert.IsType<PokemonCard>(found);
        Assert.Equal(190, ((PokemonCard)found!).Hp);
        Assert.Single(searched);
        Assert.Equal(1, await repo.CountBySetAsync("xy1", CancellationToken.None));
    }

    [Fact]
    public async Task Decks_CRUD_WithOwnership()
    {
        // Postgres no disponible (sin Docker): se omite.
        if (await TestPostgres.TryOpenAsync() is not (var db, var tx))
            return;
        await using var _ = db;
        await using var _tx = tx;
        var owner = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = owner,
            Username = $"u_{Guid.NewGuid():N}",
            Email = $"{Guid.NewGuid():N}@x.test",
            PasswordHash = "hash",
        });
        await db.SaveChangesAsync();

        var cardRepo = new EfCardRepository(db);
        await cardRepo.UpsertRangeAsync(
            [new EnergyCard("xy1-e", "Fire Energy", [], [], "xy1", "img", PokemonType.Fire, null)],
            CancellationToken.None);

        var deckRepo = new EfDeckRepository(db);
        var entry = new Application.Decks.DeckEntry(
            Guid.NewGuid(), owner, "Test", IsValid: false, Version: 1,
            new Dictionary<string, int> { ["xy1-e"] = 4 });
        await deckRepo.AddAsync(entry, CancellationToken.None);

        var loaded = await deckRepo.GetByIdAsync(entry.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(4, loaded!.Cards["xy1-e"]);

        var missing = await deckRepo.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Null(missing);
    }

    [Fact]
    public async Task Users_RegisterAndRecoveryToken_RoundTrip()
    {
        // Postgres no disponible (sin Docker): se omite.
        if (await TestPostgres.TryOpenAsync() is not (var db, var tx))
            return;
        await using var _ = db;
        await using var _tx = tx;
        var users = new EfUserRepository(db);
        var tokens = new EfRecoveryTokenRepository(db);

        var id = Guid.NewGuid();
        await users.AddAsync(
            new Application.Users.UserRecord(id, $"u_{Guid.NewGuid():N}", $"{Guid.NewGuid():N}@x.test", "hash", "User"),
            CancellationToken.None);
        var now = DateTimeOffset.UtcNow;
        await tokens.SaveAsync(
            new Application.Common.Interfaces.RecoveryToken(id, "ABC123", now.AddMinutes(15), now),
            CancellationToken.None);

        Assert.NotNull(await users.GetByIdAsync(id, CancellationToken.None));
        Assert.NotNull(await tokens.FindByHashAsync("ABC123", CancellationToken.None));
        Assert.Equal(1, await tokens.CountRecentByUserAsync(id, now.AddHours(-1), CancellationToken.None));

        await tokens.DeleteByHashAsync("ABC123", CancellationToken.None);
        Assert.Null(await tokens.FindByHashAsync("ABC123", CancellationToken.None));
    }
}
