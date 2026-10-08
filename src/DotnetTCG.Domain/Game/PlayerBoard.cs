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
    private readonly List<BoardModifier> _modifiers = [];

    public PokemonInPlay? Active { get; private set; }
    public IReadOnlyList<PokemonInPlay> Bench => _bench.AsReadOnly();
    public IReadOnlyList<Card> Hand => _hand.AsReadOnly();
    public IReadOnlyList<Card> DeckCards => _deck.ToList();
    public IReadOnlyList<Card> DiscardPile => _discardPile.AsReadOnly();
    public IReadOnlyList<Card> PrizeCards => _prizeCards.AsReadOnly();
    public IReadOnlyList<BoardModifier> Modifiers => _modifiers.AsReadOnly();
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

    public Card? RemoveFromDeck(Func<Card, bool> predicate)
    {
        var remaining = new Queue<Card>();
        Card? found = null;
        while (_deck.Count > 0)
        {
            var card = _deck.Dequeue();
            if (found is null && predicate(card))
                found = card;
            else
                remaining.Enqueue(card);
        }

        while (remaining.Count > 0)
            _deck.Enqueue(remaining.Dequeue());
        return found;
    }

    public void ShuffleDeck(Random random)
    {
        var cards = _deck.ToList();
        _deck.Clear();
        LoadDeck(cards, shuffle: true, random);
    }

    public void DiscardHand()
    {
        _discardPile.AddRange(_hand);
        _hand.Clear();
    }

    public void MillTop(int count)
    {
        for (var i = 0; i < count && _deck.Count > 0; i++)
            _discardPile.Add(_deck.Dequeue());
    }

    public Card? RecycleFirst()
    {
        if (_discardPile.Count == 0)
            return null;
        var card = _discardPile[0];
        _discardPile.RemoveAt(0);
        _hand.Add(card);
        return card;
    }

    public IReadOnlyList<Card> TakeFromDiscard(Func<Card, bool> predicate, int count)
    {
        var taken = _discardPile.Where(predicate).Take(count).ToList();
        foreach (var card in taken)
        {
            _discardPile.Remove(card);
            _hand.Add(card);
        }

        return taken;
    }

    public Card? PeekTop() => _deck.TryPeek(out var card) ? card : null;

    public IReadOnlyList<Card> InspectDeck() => _deck.ToList();

    public bool MoveDiscardToBench(PokemonCard card)
    {
        if (_bench.Count >= MaxBenchSize)
            return false;
        if (!_discardPile.Remove(card))
            return false;
        _bench.Add(new PokemonInPlay(card));
        return true;
    }

    public void Reset()
    {
        Active = null;
        _bench.Clear();
        _hand.Clear();
        _deck.Clear();
        _discardPile.Clear();
        _prizeCards.Clear();
        _modifiers.Clear();
    }

    public void RestoreState(
        IEnumerable<Card> hand,
        IEnumerable<Card> deckInOrder,
        IEnumerable<Card> discardPile,
        IEnumerable<Card> prizeCards,
        PokemonInPlay? active,
        IEnumerable<PokemonInPlay> bench,
        IEnumerable<BoardModifier> modifiers)
    {
        Reset();
        _hand.AddRange(hand);
        foreach (var card in deckInOrder)
            _deck.Enqueue(card);
        _discardPile.AddRange(discardPile);
        _prizeCards.AddRange(prizeCards);
        Active = active;
        _bench.AddRange(bench);
        _modifiers.AddRange(modifiers);
    }

    public List<Card> CollectAllCards()
    {
        var all = new List<Card>();
        all.AddRange(_hand);
        all.AddRange(_deck);
        all.AddRange(_discardPile);
        all.AddRange(_prizeCards);
        foreach (var pokemon in Field())
        {
            all.Add(pokemon.Card);
            all.AddRange(pokemon.AttachedEnergies);
            if (pokemon.AttachedTool is not null)
                all.Add(pokemon.AttachedTool);
        }

        return all;
    }

    private List<PokemonInPlay> Field()
    {
        var field = new List<PokemonInPlay>();
        if (Active is not null)
            field.Add(Active);
        field.AddRange(_bench);
        return field;
    }

    public void ClearActive()
    {
        Active = null;
    }

    public void AddModifier(BoardModifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        _modifiers.Add(modifier);
    }

    public bool ModifiersBlocked(ModifierType type, int currentTurn)
        => _modifiers.Any(m => m.Type == type &&
            (m.Duration != ModifierDuration.UntilNextTurn || currentTurn <= m.TurnApplied));

    public void RemoveExpiredModifiers(int currentTurn)
    {
        _modifiers.RemoveAll(m =>
            m.Duration == ModifierDuration.OneUse ||
            (m.Duration == ModifierDuration.UntilNextTurn && currentTurn > m.TurnApplied));
    }
}
