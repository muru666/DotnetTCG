using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Tests;

public sealed class TurnManagerTests
{
    private static GameState NewState(int startingPlayer = 0)
        => new(new PlayerBoard(), new PlayerBoard(), startingPlayer, suddenDeath: false);

    private static readonly TurnManager Turns = new();

    [Fact]
    public void StartTurn_SetsDrawAndResetsCounters()
    {
        var state = NewState();
        state.RegisterEnergyAttached();
        state.SetPhase(TurnPhase.Main);

        Turns.StartTurn(state);

        Assert.Equal(TurnPhase.Draw, state.Phase);
        Assert.Equal(0, state.EnergiesAttachedThisTurn);
    }

    [Fact]
    public void GoToMain_FromDraw_SetsMain()
    {
        var state = NewState();

        Turns.GoToMain(state);

        Assert.Equal(TurnPhase.Main, state.Phase);
    }

    [Fact]
    public void GoToMain_FromMain_Throws()
    {
        var state = NewState();
        Turns.GoToMain(state);

        Assert.Throws<InvalidOperationException>(() => Turns.GoToMain(state));
    }

    [Fact]
    public void CanGoToAttack_BlockedOnStartingPlayerFirstTurn()
    {
        var state = NewState(startingPlayer: 0);
        Turns.GoToMain(state);

        Assert.False(Turns.CanGoToAttack(state));
        Assert.Throws<InvalidOperationException>(() => Turns.GoToAttack(state));
    }

    [Fact]
    public void CanGoToAttack_AllowedAfterFirstTurn()
    {
        var state = NewState();
        Turns.EndTurn(state);
        Turns.GoToMain(state);

        Assert.True(Turns.CanGoToAttack(state));
        Turns.GoToAttack(state);

        Assert.Equal(TurnPhase.Attack, state.Phase);
    }

    [Fact]
    public void EndTurn_SwitchesPlayerIncrementsTurnAndSetsDraw()
    {
        var state = NewState();

        Turns.EndTurn(state);

        Assert.Equal(1, state.CurrentPlayerIndex);
        Assert.Equal(2, state.TurnNumber);
        Assert.Equal(TurnPhase.Draw, state.Phase);
    }

    [Fact]
    public void ShouldAutoDraw_FalseForStartingPlayerFirstTurn()
    {
        var state = NewState(startingPlayer: 0);

        Assert.False(Turns.ShouldAutoDraw(state));
    }

    [Fact]
    public void ShouldAutoDraw_TrueOtherwise()
    {
        var state = NewState();
        Turns.EndTurn(state);

        Assert.True(Turns.ShouldAutoDraw(state));
    }

    [Fact]
    public void ProcessAutoDraw_EmptyDeck_ReturnsFailure()
    {
        var state = NewState();
        Turns.EndTurn(state);

        var result = Turns.ProcessAutoDraw(state);

        Assert.True(result.IsFailure);
        Assert.Equal("Board.DeckEmpty", result.Error.Code);
    }

    [Fact]
    public void ProcessAutoDraw_WithCards_DrawsOneAndGoesToMain()
    {
        var state = NewState();
        Turns.EndTurn(state);
        var card = TestCards.BasicPokemon("xy1-1", "Pikachu", hp: 60);
        state.CurrentPlayer.LoadDeckForTests([card]);

        var result = Turns.ProcessAutoDraw(state);

        Assert.True(result.IsSuccess);
        Assert.Same(card, result.Value);
        Assert.Equal(TurnPhase.Main, state.Phase);
        Assert.Single(state.CurrentPlayer.Hand);
    }
}
