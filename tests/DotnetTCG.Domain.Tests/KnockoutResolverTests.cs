using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Engine;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Tests;

public sealed class KnockoutResolverTests
{
    private static GameState NewState(PokemonInPlay defender, PokemonInPlay? benched = null)
    {
        var state = new GameState(new PlayerBoard(), new PlayerBoard(), 0, suddenDeath: false);
        state.SetStatus(GameStatus.InProgress);
        foreach (var board in state.Players)
        {
            board.LoadDeckForTests(TestHelpers.DeckWithBasics(60));
            board.InitPrizeCards(6);
        }

        var attacker = new PokemonInPlay(TestCards.Attacker("atk", "Attacker"));
        attacker.AttachEnergy(TestCards.BasicEnergy("ae", PokemonType.Colorless));
        state.Players[0].PlaceActive(attacker);
        state.Players[1].PlaceActive(defender);
        if (benched is not null)
            state.Players[1].AddToBench(benched);
        state.SetPhase(TurnPhase.Main);
        return state;
    }

    private static GameState AttackUntilKO(GameState state, PokemonInPlay defender)
    {
        var engine = new GameEngine(GameEngine.CreateDefault(new Random(17),
            new SeededCoinFlip(Enumerable.Repeat(true, 1000).ToArray())));
        while (!defender.KnockedOut && state.Status == GameStatus.InProgress)
        {
            state.AdvanceTurn();
            state.SetCurrentPlayer(0);
            state.SetPhase(TurnPhase.Main);
            var result = engine.Execute(state, new GameAction(GameActionType.Attack, 0,
                Attack: new Attack("Tackle", "", "30", [PokemonType.Colorless])));
            if (!result.Success && result.Error is "Attack.FirstTurn" or "Attack.EnergyShort")
                continue;
        }

        return state;
    }

    [Fact]
    public void Knockout_RegularPokemon_TakesOnePrize()
    {
        var defender = new PokemonInPlay(TestCards.BasicPokemon("d", "Defender", hp: 60));
        var state = NewState(defender);

        AttackUntilKO(state, defender);

        Assert.Equal(5, state.Players[0].PrizeCount);
        Assert.Contains(state.Players[1].DiscardPile, c => c.Id == "d");
    }

    [Fact]
    public void Knockout_ExPokemon_TakesTwoPrizes()
    {
        var exCard = TestCards.BasicPokemon("ex", "Big EX", hp: 60) with
        {
            Subtypes = [SubType.Basic, SubType.Ex],
        };
        var defender = new PokemonInPlay(exCard);
        var state = NewState(defender);

        AttackUntilKO(state, defender);

        Assert.Equal(4, state.Players[0].PrizeCount);
    }

    [Fact]
    public void Knockout_PromotesFirstBenched()
    {
        var defender = new PokemonInPlay(TestCards.BasicPokemon("d", "Defender", hp: 60));
        var bench = new PokemonInPlay(TestCards.BasicPokemon("b", "Bench", hp: 60));
        var state = NewState(defender, bench);

        AttackUntilKO(state, defender);

        Assert.Same(bench, state.Players[1].Active);
        Assert.Empty(state.Players[1].Bench);
        Assert.False(bench.JustPlayed); // la promoción no cuenta como "recién jugado"
    }

    [Fact]
    public void Knockout_WithoutBench_OpponentLoses()
    {
        var defender = new PokemonInPlay(TestCards.BasicPokemon("d", "Defender", hp: 60));
        var state = NewState(defender);

        AttackUntilKO(state, defender);

        Assert.Null(state.Players[1].Active);
        Assert.Equal(GameStatus.Finished, state.Status);
        Assert.Equal(0, state.WinnerPlayerIndex);
    }

    [Fact]
    public void Knockout_DiscardsAttachedEnergy()
    {
        var defender = new PokemonInPlay(TestCards.BasicPokemon("d", "Defender", hp: 60));
        defender.AttachEnergy(TestCards.BasicEnergy("de", PokemonType.Fire));
        var state = NewState(defender);

        AttackUntilKO(state, defender);

        Assert.Contains(state.Players[1].DiscardPile, c => c.Id == "de");
        Assert.Empty(defender.AttachedEnergies);
    }
}
