using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Application.Decks;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Application.Tests;

public sealed class DeckServiceTests
{
    private static (DeckService Service, FakeDeckRepository Decks) NewService()
    {
        var decks = new FakeDeckRepository();
        var service = new DeckService(decks, new FakeCardRepository(TestCardFactory.Catalog()), new RuleValidator());
        return (service, decks);
    }

    [Fact]
    public async Task Create_DuplicateName_Conflict()
    {
        var (service, _) = NewService();
        var owner = Guid.NewGuid();

        var first = await service.CreateAsync(owner, "My Deck", CancellationToken.None);
        var second = await service.CreateAsync(owner, "My Deck", CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal("Deck.DuplicateName", second.Error.Code);
    }

    [Fact]
    public async Task Get_ForeignDeck_NotFound()
    {
        var (service, _) = NewService();
        var created = await service.CreateAsync(Guid.NewGuid(), "Mine", CancellationToken.None);

        var result = await service.GetAsync(created.Value!.Id, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Deck.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task AddCard_MissingCard_NotFound()
    {
        var (service, _) = NewService();
        var owner = Guid.NewGuid();
        var created = await service.CreateAsync(owner, "D", CancellationToken.None);

        var result = await service.AddCardAsync(created.Value!.Id, owner, "nope", 1, CancellationToken.None);

        Assert.Equal("Card.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task AddCard_Exceeding60_Rejected()
    {
        var (service, _) = NewService();
        var owner = Guid.NewGuid();
        var created = await service.CreateAsync(owner, "D", CancellationToken.None);
        var id = created.Value!.Id;

        for (var i = 0; i < 10; i++)
            await service.AddCardAsync(id, owner, $"xy1-p{i}", 4, CancellationToken.None);
        for (var i = 0; i < 4; i++)
            await service.AddCardAsync(id, owner, $"xy1-s{i}", 4, CancellationToken.None);
        var ok = await service.AddCardAsync(id, owner, "xy1-e", 4, CancellationToken.None);
        Assert.True(ok.IsSuccess);

        var over = await service.AddCardAsync(id, owner, "xy1-e", 1, CancellationToken.None);

        Assert.Equal("Deck.TooMany", over.Error.Code);
    }

    [Fact]
    public async Task Validate_Golden60_Valid()
    {
        var (service, _) = NewService();
        var owner = Guid.NewGuid();
        var created = await service.CreateAsync(owner, "D", CancellationToken.None);
        var id = created.Value!.Id;

        foreach (var (cardId, qty) in TestCardFactory.Golden60())
            await service.AddCardAsync(id, owner, cardId, qty, CancellationToken.None);

        var result = await service.ValidateAsync(id, owner, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Valid);
        Assert.Empty(result.Value.Errors);
    }

    [Fact]
    public async Task Validate_FiveCopies_Invalid()
    {
        var decks = new FakeDeckRepository();
        var cards = TestCardFactory.Catalog();
        var service = new DeckService(decks, new FakeCardRepository(cards), new RuleValidator());
        var owner = Guid.NewGuid();
        var created = await service.CreateAsync(owner, "D", CancellationToken.None);

        var merged = new Dictionary<string, int>(TestCardFactory.Golden60()) { ["xy1-p0"] = 5 };
        merged["xy1-e"] = 3;
        await decks.UpdateAsync((await decks.GetByIdAsync(created.Value!.Id, CancellationToken.None))!
            with { Cards = merged }, CancellationToken.None);

        var result = await service.ValidateAsync(created.Value.Id, owner, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Valid);
    }

    [Fact]
    public async Task Validate_TwoAceSpecs_Invalid()
    {
        var cards = TestCardFactory.Catalog();
        cards["xy1-a1"] = TestCardFactory.AceSpec("xy1-a1", "Ace One");
        cards["xy1-a2"] = TestCardFactory.AceSpec("xy1-a2", "Ace Two");
        var decks = new FakeDeckRepository();
        var service = new DeckService(decks, new FakeCardRepository(cards), new RuleValidator());
        var owner = Guid.NewGuid();
        var created = await service.CreateAsync(owner, "D", CancellationToken.None);

        var merged = new Dictionary<string, int>(TestCardFactory.Golden60())
        {
            ["xy1-a1"] = 1,
            ["xy1-a2"] = 1,
            ["xy1-e"] = 2,
        };
        await decks.UpdateAsync((await decks.GetByIdAsync(created.Value!.Id, CancellationToken.None))!
            with { Cards = merged }, CancellationToken.None);

        var result = await service.ValidateAsync(created.Value.Id, owner, CancellationToken.None);

        Assert.False(result.Value!.Valid);
        Assert.Contains(result.Value.Errors, e => e.Contains("ACE SPEC"));
    }

    [Fact]
    public async Task Delete_ForeignDeck_NotFound()
    {
        var (service, _) = NewService();
        var created = await service.CreateAsync(Guid.NewGuid(), "D", CancellationToken.None);

        var result = await service.DeleteAsync(created.Value!.Id, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("Deck.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task EnsureStarterDeck_CreatesBalanced60_ThenSkips()
    {
        var cards = new Dictionary<string, Card>();
        for (var i = 0; i < 5; i++)
            cards[$"xy1-p{i}"] = TestCardFactory.Pokemon($"xy1-p{i}", $"Poke{i}");
        for (var i = 0; i < 5; i++)
            cards[$"xy1-s{i}"] = TestCardFactory.Supporter($"xy1-s{i}", $"Supporter{i}");
        cards["xy1-e"] = TestCardFactory.Energy("xy1-e");
        var decks = new FakeDeckRepository();
        var service = new DeckService(decks, new FakeCardRepository(cards), new RuleValidator());
        var owner = Guid.NewGuid();

        var created = await service.EnsureStarterDeckAsync(owner, CancellationToken.None);

        Assert.NotNull(created);
        Assert.Equal(DeckService.StarterDeckName, created.Name);
        Assert.Equal(60, created.Cards.Values.Sum());
        Assert.Equal(20, created.Cards.Where(kv => cards[kv.Key] is PokemonCard).Sum(kv => kv.Value));
        Assert.Equal(20, created.Cards.Where(kv => cards[kv.Key] is TrainerCard).Sum(kv => kv.Value));
        Assert.Equal(20, created.Cards.Where(kv => cards[kv.Key] is EnergyCard).Sum(kv => kv.Value));

        var second = await service.EnsureStarterDeckAsync(owner, CancellationToken.None);

        Assert.Null(second);
        Assert.Single(await decks.ListByOwnerAsync(owner, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureStarterDeck_InsufficientCatalog_ReturnsNull()
    {
        var (service, _) = NewService();
        var created = await service.EnsureStarterDeckAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(created);
    }
}
