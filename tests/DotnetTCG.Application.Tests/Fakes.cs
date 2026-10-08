using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Application.Decks;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Application.Tests;

internal sealed class FakeCardRepository(Dictionary<string, Card>? cards = null) : ICardRepository
{
    private readonly Dictionary<string, Card> _cards = cards ?? [];

    public int UpsertedCount { get; private set; }

    public Task<Card?> GetByIdAsync(string id, CancellationToken cancellationToken)
        => Task.FromResult(_cards.GetValueOrDefault(id));

    public Task<IReadOnlyDictionary<string, Card>> GetByIdsAsync(
        IEnumerable<string> ids, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyDictionary<string, Card>>(
            ids.Distinct().Where(_cards.ContainsKey).ToDictionary(id => id, id => _cards[id]));

    public Task<IReadOnlyList<Card>> SearchAsync(
        string? name, PokemonType? type, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _cards.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(c => c.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<IReadOnlyList<Card>>(
            query.Skip(page * pageSize).Take(pageSize).ToList());
    }

    public Task<int> CountAsync(string? name, PokemonType? type, CancellationToken cancellationToken)
        => Task.FromResult(_cards.Count);

    public Task UpsertRangeAsync(IEnumerable<Card> cards, CancellationToken cancellationToken)
    {
        foreach (var card in cards)
        {
            _cards[card.Id] = card;
            UpsertedCount++;
        }

        return Task.CompletedTask;
    }

    public Task<int> CountBySetAsync(string setId, CancellationToken cancellationToken)
        => Task.FromResult(_cards.Values.Count(c => c.SetId == setId));
}

internal sealed class FakeDeckRepository : IDeckRepository
{
    private readonly Dictionary<Guid, DeckEntry> _decks = [];

    public Task<DeckEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_decks.GetValueOrDefault(id));

    public Task<IReadOnlyList<DeckEntry>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<DeckEntry>>(
            _decks.Values.Where(d => d.OwnerId == ownerId).ToList());

    public Task<DeckEntry?> FindByOwnerAndNameAsync(Guid ownerId, string name, CancellationToken cancellationToken)
        => Task.FromResult(_decks.Values.FirstOrDefault(d => d.OwnerId == ownerId && d.Name == name));

    public Task AddAsync(DeckEntry deck, CancellationToken cancellationToken)
    {
        _decks[deck.Id] = deck;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(DeckEntry deck, CancellationToken cancellationToken)
    {
        var existing = _decks[deck.Id];
        _decks[deck.Id] = deck with { Version = existing.Version + 1 };
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        _decks.Remove(id);
        return Task.CompletedTask;
    }
}

internal sealed class FakePokeApiClient(IReadOnlyList<Card> cards) : IPokeApiClient
{
    public Task<IReadOnlyList<Card>> FetchSetAsync(string setId, CancellationToken cancellationToken)
        => Task.FromResult(cards);
}

internal static class TestCardFactory
{
    public static PokemonCard Pokemon(string id, string name, bool basic = true)
        => new(id, name, [basic ? SubType.Basic : SubType.Stage1], [], "xy1", string.Empty,
            Hp: 60, RetreatCost: 1, EvolvesFrom: basic ? null : "Base",
            PokemonType.Colorless, [], [], [], []);

    public static EnergyCard Energy(string id, string name = "Fire Energy")
        => new(id, name, [], [], "xy1", string.Empty, PokemonType.Fire, Special: null);

    public static TrainerCard Supporter(string id, string name)
        => new(id, name, [SubType.Supporter], [], "xy1", string.Empty, SubType.Supporter);

    public static TrainerCard AceSpec(string id, string name)
        => new(id, name, [SubType.AceSpec], [], "xy1", string.Empty, SubType.AceSpec);

    public static Dictionary<string, Card> Catalog()
    {
        var cards = new Dictionary<string, Card>();
        for (var i = 0; i < 10; i++)
            cards[$"xy1-p{i}"] = Pokemon($"xy1-p{i}", $"Poke{i}");
        for (var i = 0; i < 4; i++)
            cards[$"xy1-s{i}"] = Supporter($"xy1-s{i}", $"Supporter{i}");
        cards["xy1-e"] = Energy("xy1-e");
        return cards;
    }

    public static Dictionary<string, int> Golden60()
    {
        var deck = new Dictionary<string, int>();
        for (var i = 0; i < 10; i++)
            deck[$"xy1-p{i}"] = 4;
        for (var i = 0; i < 4; i++)
            deck[$"xy1-s{i}"] = 4;
        deck["xy1-e"] = 4;
        return deck;
    }

    public static PokemonCard Attacker(string id, string name)
        => new(id, name, [SubType.Basic], [], "xy1", string.Empty,
            Hp: 60, RetreatCost: 1, EvolvesFrom: null, PokemonType.Colorless,
            [new Attack("Tackle", string.Empty, "30", [PokemonType.Colorless])],
            [], [], []);

}
