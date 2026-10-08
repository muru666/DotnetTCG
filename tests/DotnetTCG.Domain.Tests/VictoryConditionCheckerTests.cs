using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Tests;

public sealed class VictoryConditionCheckerTests
{
    private static readonly VictoryConditionChecker Victories = new();

    private static GameState NewOngoingState()
    {
        var state = new GameState(new PlayerBoard(), new PlayerBoard(), 0, suddenDeath: false);
        state.SetStatus(GameStatus.InProgress);
        foreach (var board in state.Players)
        {
            board.LoadDeckForTests(TestHelpers.DeckWithBasics(60));
            for (var i = 0; i < 7; i++)
                board.DrawCard();
            board.PlaceActive(new PokemonInPlay(TestCards.BasicPokemon("a", "A", hp: 60)));
            board.InitPrizeCards(6);
        }
        return state;
    }

    [Fact]
    public void Check_OngoingGame_NotFinished()
    {
        var result = Victories.Check(NewOngoingState());

        Assert.False(result.GameOver);
        Assert.Null(result.WinnerPlayerIndex);
        Assert.False(result.RequiresSuddenDeath);
    }

    [Fact]
    public void Check_LastPrizeTaken_Wins()
    {
        var state = NewOngoingState();
        while (state.Players[0].PrizeCount > 0)
            state.Players[0].TakePrize();

        var result = Victories.Check(state);

        Assert.True(result.GameOver);
        Assert.Equal(0, result.WinnerPlayerIndex);
    }

    [Fact]
    public void Check_BothPrizesEmpty_SuddenDeath()
    {
        var state = NewOngoingState();
        while (state.Players[0].PrizeCount > 0)
            state.Players[0].TakePrize();
        while (state.Players[1].PrizeCount > 0)
            state.Players[1].TakePrize();

        var result = Victories.Check(state);

        Assert.False(result.GameOver);
        Assert.True(result.RequiresSuddenDeath);
    }

    [Fact]
    public void Check_NoFieldPokemon_OpponentWins()
    {
        var state = NewOngoingState();
        state.Players[1].ClearActive();

        var result = Victories.Check(state);

        Assert.True(result.GameOver);
        Assert.Equal(0, result.WinnerPlayerIndex);
    }
}
