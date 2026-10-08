using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Tests;

internal static class TestHelpers
{
    public static List<Card> DeckWithBasics(int size, int basics = 10)
    {
        var cards = new List<Card>();
        for (var i = 0; i < basics; i++)
            cards.Add(TestCards.BasicPokemon($"xy1-{i + 1}", $"Poke{i}", hp: 60));
        for (var i = basics; i < size; i++)
            cards.Add(TestCards.BasicEnergy($"e-{i}", PokemonType.Colorless));
        return cards;
    }

    public static List<Card> DeckWithoutBasics(int size)
    {
        var cards = new List<Card>();
        for (var i = 0; i < size; i++)
            cards.Add(TestCards.BasicEnergy($"e-{i}", PokemonType.Colorless));
        return cards;
    }
}

public sealed class SetupServiceTests
{
    private static readonly SetupService Setup = new();

    private static GameState NewState()
        => new(new PlayerBoard(), new PlayerBoard(), 0, suddenDeath: false);

    [Fact]
    public void InitializeDeal_DrawsSevenEach()
    {
        var state = NewState();

        Setup.InitializeDeal(state, TestHelpers.DeckWithBasics(60), TestHelpers.DeckWithBasics(60), new Random(42));

        Assert.Equal(7, state.Players[0].Hand.Count);
        Assert.Equal(7, state.Players[1].Hand.Count);
        Assert.Equal(53, state.Players[0].DeckCount);
    }

    [Fact]
    public void NeedsMulligan_NoBasic_True()
    {
        var state = NewState();
        state.Players[0].LoadDeckForTests(TestHelpers.DeckWithoutBasics(60));
        for (var i = 0; i < 7; i++)
            state.Players[0].DrawCard();

        Assert.True(Setup.NeedsMulligan(state.Players[0]));
    }

    [Fact]
    public void HandleMulligan_RedrawsSevenAndRivalDrawsOne()
    {
        var state = NewState();
        Setup.InitializeDeal(state, TestHelpers.DeckWithoutBasics(60), TestHelpers.DeckWithBasics(60), new Random(7));

        Setup.HandleMulligan(state, 0, new Random(7));

        Assert.Equal(7, state.Players[0].Hand.Count);
        Assert.Equal(8, state.Players[1].Hand.Count);
        Assert.Equal(1, state.MulliganCounts[0]);
    }

    [Fact]
    public void PlaceActive_NonBasic_ReturnsError()
    {
        var state = NewState();
        var stage1 = TestCards.Stage1Pokemon("xy1-2", "Raichu", "Pikachu", hp: 90);

        Assert.Equal("Setup.NotBasic", Setup.PlaceActive(state, 0, stage1)?.Code);
    }

    [Fact]
    public void PlaceBench_BeforeActive_ReturnsError()
    {
        var state = NewState();
        state.Players[0].AddToHand(TestCards.BasicPokemon("xy1-1", "Pikachu", hp: 60));

        Assert.Equal("Setup.NoActive",
            Setup.PlaceBench(state, 0, (PokemonCard)state.Players[0].Hand[0])?.Code);
    }

    [Fact]
    public void ConfirmSetup_FirstPlayer_PassesTurn()
    {
        var state = NewState();
        var basic = TestCards.BasicPokemon("xy1-1", "Pikachu", hp: 60);
        state.Players[0].AddToHand(basic);
        var basic1 = TestCards.BasicPokemon("xy1-3", "Bulbasaur", hp: 60);
        state.Players[1].AddToHand(basic1);
        Assert.Null(Setup.PlaceActive(state, 0, basic));
        Assert.Null(Setup.PlaceActive(state, 1, basic1));

        Assert.Null(Setup.ConfirmSetup(state, 0));

        Assert.Equal(1, state.CurrentPlayerIndex);
        Assert.False(state.AllSetupConfirmed);
    }

    [Fact]
    public void FinalizeSetup_SixPrizesAndInProgress()
    {
        var state = NewState();
        Setup.InitializeDeal(state, TestHelpers.DeckWithBasics(60), TestHelpers.DeckWithBasics(60), new Random(3));

        Setup.FinalizeSetup(state);

        Assert.Equal(6, state.Players[0].PrizeCount);
        Assert.Equal(6, state.Players[1].PrizeCount);
        Assert.Equal(GameStatus.InProgress, state.Status);
        Assert.Equal(TurnPhase.Draw, state.Phase);
    }

    [Fact]
    public void FinalizeSetup_SuddenDeath_OnePrize()
    {
        var state = new GameState(new PlayerBoard(), new PlayerBoard(), 0, suddenDeath: true);
        Setup.InitializeDeal(state, TestHelpers.DeckWithBasics(60), TestHelpers.DeckWithBasics(60), new Random(3));

        Setup.FinalizeSetup(state);

        Assert.Equal(1, state.Players[0].PrizeCount);
        Assert.Equal(1, state.Players[1].PrizeCount);
    }
}
