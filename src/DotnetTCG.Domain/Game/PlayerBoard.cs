using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Common;
using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Domain.Game;

public sealed class PlayerBoard
{
    public const int MaxBenchSize = 5;

    private readonly List<PokemonInPlay> _bench = [];
    private readonly List<Card> _hand = [];
    private readonly Queue<Card> _deck = [];
    private readonly List<Card> _discardPile = [];
    private readonly List<Card> _prizeCards = [];

    public PokemonInPlay? Active { get; private set; }
    public IReadOnlyList<PokemonInPlay> Bench => _bench.AsReadOnly();
    public IReadOnlyList<Card> Hand => _hand.AsReadOnly();
    public IReadOnlyList<Card> DiscardPile => _discardPile.AsReadOnly();
    public int DeckCount => _deck.Count;
    public int PrizeCount => _prizeCards.Count;

    public void LoadDeck(IEnumerable<Card> cards, bool shuffle, Random random)
    {
        var ordered = cards.ToList();
        if (shuffle)
        {
            for (var i = ordered.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (ordered[i], ordered[j]) = (ordered[j], ordered[i]);
            }
        }

        _deck.Clear();
        foreach (var card in ordered)
            _deck.Enqueue(card);
    }

    public void LoadDeckForTests(IEnumerable<Card> cards)
    {
        _deck.Clear();
        foreach (var card in cards)
            _deck.Enqueue(card);
    }

    public Result<Card> DrawCard()
    {
        if (_deck.Count == 0)
            return Result<Card>.Failure(new Error("Board.DeckEmpty", "Deck is empty."));
        var card = _deck.Dequeue();
        _hand.Add(card);
        return Result<Card>.Success(card);
    }

    public void AddToHand(Card card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _hand.Add(card);
    }

    public bool RemoveFromHand(Card card) => _hand.Remove(card);

    public void PlaceActive(PokemonInPlay pokemon)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        if (Active is not null)
            throw new InvalidOperationException("Active spot is already occupied.");
        Active = pokemon;
    }

    public void AddToBench(PokemonInPlay pokemon)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        if (_bench.Count >= MaxBenchSize)
            throw new InvalidOperationException("Bench is full.");
        _bench.Add(pokemon);
    }

    public void RemoveFromBench(PokemonInPlay pokemon) => _bench.Remove(pokemon);

    public void SwitchActive(PokemonInPlay replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        if (Active is null)
            throw new InvalidOperationException("No active pokemon to switch.");
        if (!_bench.Remove(replacement))
            throw new InvalidOperationException("Replacement must come from the bench.");
        var previous = Active;
        Active = replacement;
        _bench.Add(previous);
    }

    public void Discard(Card card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _discardPile.Add(card);
    }

    public void InitPrizeCards(int count)
    {
        for (var i = 0; i < count && _deck.Count > 0; i++)
            _prizeCards.Add(_deck.Dequeue());
    }

    public Result<Card> TakePrize()
    {
        if (_prizeCards.Count == 0)
            return Result<Card>.Failure(new Error("Board.NoPrizes", "No prize cards left."));
        var prize = _prizeCards[^1];
        _prizeCards.RemoveAt(_prizeCards.Count - 1);
        _hand.Add(prize);
        return Result<Card>.Success(prize);
    }

    public void ShuffleHandIntoDeck(Random random)
    {
        var hand = _hand.ToList();
        _hand.Clear();
        var combined = _deck.ToList();
        combined.AddRange(hand);
        _deck.Clear();
        LoadDeck(combined, shuffle: true, random);
    }

    public void ClearActive()
    {
        Active = null;
    }
}
