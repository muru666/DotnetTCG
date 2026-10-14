using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Tests;

public sealed class EffectEngineCoverageTests
{
    private readonly StatusEffectManager _statuses = new();

    private EffectEngine Engine(ICoinFlipService? coin = null)
        => new(_statuses, coin ?? new SeededCoinFlip(Enumerable.Repeat(true, 50).ToArray()),
            new Random(3));

    private static GameState NewState()
    {
        var state = new GameState(new PlayerBoard(), new PlayerBoard(), 0, suddenDeath: false);
        state.SetStatus(GameStatus.InProgress);
        foreach (var board in state.Players)
        {
            board.LoadDeckForTests(TestHelpers.DeckWithBasics(60));
            board.PlaceActive(new PokemonInPlay(TestCards.BasicPokemon("a", "A", hp: 100)));
        }

        return state;
    }

    private static EffectContext Own(GameState state, PokemonInPlay? source = null)
        => new(state, 0, source ?? state.Players[0].Active);

    [Fact]
    public void ShuffleHand_ReplacesHandFromDeck()
    {
        var state = NewState();
        var board = state.Players[0];
        for (var i = 0; i < 3; i++)
            board.AddToHand(TestCards.BasicEnergy($"h{i}", PokemonType.Fire));

        Engine().Apply(Own(state), new EffectSpec(
            EffectType.ShuffleHand, Target: EffectTarget.SelfPlayer));

        Assert.Empty(board.Hand);
        Assert.True(board.DeckCount > 50);
    }

    [Fact]
    public void DiscardHand_DumpsHandToDiscard()
    {
        var state = NewState();
        var board = state.Players[0];
        board.AddToHand(TestCards.BasicEnergy("h", PokemonType.Fire));

        Engine().Apply(Own(state), new EffectSpec(
            EffectType.DiscardHand, Target: EffectTarget.SelfPlayer));

        Assert.Empty(board.Hand);
        Assert.Contains(board.DiscardPile, c => c.Id == "h");
    }

    [Fact]
    public void HealAll_FullFieldHeal()
    {
        var state = NewState();
        state.Players[0].Active!.AddDamage(70);
        var bench = new PokemonInPlay(TestCards.BasicPokemon("b", "B", hp: 60));
        bench.AddDamage(30);
        state.Players[0].AddToBench(bench);

        Engine().Apply(Own(state), new EffectSpec(EffectType.HealAll, Target: EffectTarget.AllOwn));

        Assert.Equal(0, state.Players[0].Active.TotalDamage);
        Assert.Equal(0, bench.TotalDamage);
    }

    [Fact]
    public void SearchPokemon_TakesFromDeckAndShufflesBack()
    {
        var state = NewState();
        var deckBefore = state.Players[0].DeckCount;

        Engine().Apply(Own(state), new EffectSpec(EffectType.SearchPokemon, Target: EffectTarget.SelfPlayer));

        Assert.Single(state.Players[0].Hand);
        Assert.Equal(deckBefore - 1, state.Players[0].DeckCount);
    }

    [Fact]
    public void Revive_PlacesDiscardedBasicOnBench()
    {
        var state = NewState();
        var board = state.Players[0];
        var basic = TestCards.BasicPokemon("dead", "Revived", hp: 60);
        board.Discard(basic);

        Engine().Apply(Own(state), new EffectSpec(EffectType.Revive, Target: EffectTarget.SelfPlayer));

        Assert.Contains(board.Bench, p => p.Card.Id == "dead");
        Assert.DoesNotContain(board.DiscardPile, c => c.Id == "dead");
    }

    [Fact]
    public void Revive_UnknownInDiscard_NoChange()
    {
        var state = NewState();

        Engine().Apply(Own(state), new EffectSpec(EffectType.Revive, Target: EffectTarget.SelfPlayer));

        Assert.Empty(state.Players[0].Bench);
    }

    [Fact]
    public void DiscardDeck_MillsTopCards()
    {
        var state = NewState();
        var board = state.Players[0];
        var before = board.DeckCount;

        Engine().Apply(Own(state), new EffectSpec(
            EffectType.DiscardDeck, Target: EffectTarget.SelfPlayer, Amount: 4));

        Assert.Equal(before - 4, board.DeckCount);
        Assert.Equal(4, board.DiscardPile.Count);
    }

    [Fact]
    public void DiscardAll_RemovesAllMatchingEnergiesToDiscard()
    {
        var state = NewState();
        var active = state.Players[0].Active!;
        active.AttachEnergy(TestCards.BasicEnergy("f1", PokemonType.Fire));
        active.AttachEnergy(TestCards.BasicEnergy("f2", PokemonType.Fire));
        active.AttachEnergy(TestCards.BasicEnergy("w1", PokemonType.Water));

        Engine().Apply(Own(state), new EffectSpec(
            EffectType.DiscardAll, Target: EffectTarget.OwnActive, EnergyType: PokemonType.Fire));

        Assert.Single(active.AttachedEnergies);
        Assert.Equal(2, state.Players[0].DiscardPile.Count(c => c.Supertype == SuperType.Energy));
    }

    [Fact]
    public void DrawShuffle_CyclesHandIntoDeck()
    {
        var state = NewState();
        var board = state.Players[0];
        for (var i = 0; i < 4; i++)
            board.AddToHand(TestCards.BasicEnergy($"h{i}", PokemonType.Fire));
        var deckBefore = board.DeckCount;

        Engine().Apply(Own(state), new EffectSpec(
            EffectType.DrawShuffle, Target: EffectTarget.SelfPlayer));

        Assert.Equal(4, board.Hand.Count);
        Assert.Equal(deckBefore, board.DeckCount);
    }

    [Fact]
    public void DiscardOpponentDeck_MillsOpponent()
    {
        var state = NewState();
        var opponent = state.Players[1];
        var before = opponent.DeckCount;

        Engine().Apply(Own(state), new EffectSpec(
            EffectType.DiscardOpponentDeck, Target: EffectTarget.OpponentPlayer, Amount: 3));

        Assert.Equal(before - 3, opponent.DeckCount);
        Assert.Equal(3, opponent.DiscardPile.Count);
    }

    [Fact]
    public void PreventRetreat_BlocksOpponentRetreat()
    {
        var state = NewState();

        Engine().Apply(Own(state), new EffectSpec(
            EffectType.PreventRetreat, Target: EffectTarget.OpponentPlayer));

        var rules = new RuleValidator();
        state.Players[1].AddToBench(new PokemonInPlay(TestCards.BasicPokemon("b", "B", hp: 60)));
        state.Players[1].Active!.AttachEnergy(TestCards.BasicEnergy("e", PokemonType.Fire));
        var errors = rules.CanRetreat(state, 1, state.Players[1].Active.AttachedEnergies.ToList());
        Assert.NotNull(errors);
        Assert.Equal("Retreat.Blocked", errors!.Code);
    }

    [Fact]
    public void RestrictAttack_BlocksOpponentAttack()
    {
        var state = NewState();
        state.AdvanceTurn();

        Engine().Apply(Own(state), new EffectSpec(
            EffectType.RestrictAttack, Target: EffectTarget.OpponentActive));

        var defender = state.Players[1].Active!;
        Assert.True(defender.HasModifier(ModifierType.CannotAttack, state.TurnNumber));

        var rules = new RuleValidator();
        var attack = new Attack("Tackle", "", "30", [PokemonType.Colorless]);
        Assert.Equal("Attack.Blocked", rules.CanAttack(state, 1, attack)?.Code);
    }

    [Fact]
    public void FlipEnergy_Tails_DoesNotAttach()
    {
        var state = NewState();
        var active = state.Players[0].Active!;

        Engine(new SeededCoinFlip(false)).Apply(Own(state, active),
            new EffectSpec(EffectType.FlipEnergy, EnergyType: PokemonType.Fire));

        Assert.Empty(active.AttachedEnergies);
    }

    [Fact]
    public void SearchAny_WithSubtype_TakesSupporterOnly()
    {
        var state = NewState();
        // El mazo de test tiene básicos y energías; sin supporter → no toma nada.
        Engine().Apply(Own(state), new EffectSpec(
            EffectType.SearchAny, Target: EffectTarget.SelfPlayer, TrainerSubtype: SubType.Supporter));

        Assert.Empty(state.Players[0].Hand);
    }

    [Fact]
    public void SearchAny_NoMatch_ShufflesDeckBack()
    {
        var state = NewState();
        var before = state.Players[0].DeckCount;

        Engine().Apply(Own(state), new EffectSpec(
            EffectType.SearchAny, Target: EffectTarget.SelfPlayer, CardName: "Not in deck"));

        Assert.Empty(state.Players[0].Hand);
        Assert.Equal(before, state.Players[0].DeckCount);
    }

    [Fact]
    public void MoveEnergy_NoEnergyAttached_NoChange()
    {
        var state = NewState();
        var bench = new PokemonInPlay(TestCards.BasicPokemon("b", "B", hp: 60));
        state.Players[0].AddToBench(bench);

        Engine().Apply(new EffectContext(state, 0, state.Players[0].Active, bench),
            new EffectSpec(EffectType.MoveEnergy, EnergyType: PokemonType.Fire));

        Assert.Empty(bench.AttachedEnergies);
        Assert.Empty(state.Players[0].Active!.AttachedEnergies);
    }

    [Fact]
    public void FlipCoin_Type_IsNoOp()
    {
        var state = NewState();

        Engine().Apply(Own(state), new EffectSpec(EffectType.FlipCoin));

        Assert.Equal(0, state.Players[0].Active!.TotalDamage);
    }

    [Fact]
    public void OpponentSwitch_NoBench_NoChange()
    {
        var state = NewState();
        var opponentActive = state.Players[1].Active;

        Engine().Apply(Own(state), new EffectSpec(EffectType.OpponentSwitch));

        Assert.Same(opponentActive, state.Players[1].Active);
    }

    [Fact]
    public void SwitchActive_NoBench_NoChange()
    {
        var state = NewState();
        var active = state.Players[0].Active;

        Engine().Apply(Own(state), new EffectSpec(EffectType.SwitchActive, Target: EffectTarget.OwnBench));

        Assert.Same(active, state.Players[0].Active);
    }

    [Fact]
    public void Recycle_EmptyDiscard_NoChange()
    {
        var state = NewState();

        Engine().Apply(Own(state), new EffectSpec(EffectType.Recycle));

        Assert.Empty(state.Players[0].Hand);
    }
}
