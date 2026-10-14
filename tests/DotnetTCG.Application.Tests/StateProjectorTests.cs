using DotnetTCG.Application.Games;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Application.Tests;

public sealed class StateProjectorTests
{
    private static GameState NewState()
    {
        var state = new GameState(new PlayerBoard(), new PlayerBoard(), 0, suddenDeath: false);
        state.SetStatus(GameStatus.InProgress);
        for (var i = 0; i < 2; i++)
        {
            var board = state.Players[i];
            var active = new PokemonInPlay(TestCardFactory.Pokemon($"p{i}", "Active"));
            active.AddDamage(30);
            active.AddStatus(Status.Burned);
            board.PlaceActive(active);
            board.AddToHand(TestCardFactory.Energy($"e{i}"));
            board.AddToHand(TestCardFactory.Supporter($"s{i}", "Tierno"));
            board.LoadDeckForTests([TestCardFactory.Pokemon("d0", "DeckCard")]);
        }

        state.SetPendingChoice(new PendingChoice("choice", 1, null, "Pick", [new ChoiceOption("0", "X")]));
        return state;
    }

    [Fact]
    public void Project_ViewerSeesOnlyOwnHand()
    {
        var state = NewState();

        var view = StateProjector.Project(state, viewerOrder: 0);

        Assert.NotNull(view.Boards[0].Hand);
        Assert.Null(view.Boards[1].Hand);
        Assert.Equal(2, view.Boards[0].Hand!.Count);
        Assert.Equal(2, view.Boards[1].HandCount);
    }

    [Fact]
    public void Project_ViewerOne_SymmetricView()
    {
        var state = NewState();

        var view = StateProjector.Project(state, viewerOrder: 1);

        Assert.Null(view.Boards[0].Hand);
        Assert.NotNull(view.Boards[1].Hand);
        Assert.Equal(2, view.Boards[1].Hand!.Count);
    }

    [Fact]
    public void Project_Spectator_NoHandsAtAll()
    {
        var state = NewState();

        var view = StateProjector.Project(state, viewerOrder: null);

        Assert.Null(view.Boards[0].Hand);
        Assert.Null(view.Boards[1].Hand);
        Assert.Equal(2, view.Boards[0].HandCount);
        Assert.Equal(2, view.Boards[1].HandCount);
    }

    [Fact]
    public void Project_PendingChoice_OnlyForItsPlayer()
    {
        var state = NewState();

        var forOwner = StateProjector.Project(state, viewerOrder: 1);
        var forRival = StateProjector.Project(state, viewerOrder: 0);

        Assert.NotNull(forOwner.PendingChoice);
        Assert.Null(forRival.PendingChoice);
    }

    [Fact]
    public void Project_FieldsAlwaysVisible_NoLeakOfHiddenData()
    {
        var state = NewState();

        var view = StateProjector.Project(state, viewerOrder: 1);

        var rival = view.Boards[0];
        Assert.NotNull(rival.Active);
        Assert.Equal(3, rival.Active!.DamageCounters); // 30 de daño = 3 contadores
        Assert.Equal(0, rival.Active.TotalEnergy); // sin energías unidas en el fixture
        Assert.Single(rival.Active.Statuses);
        Assert.Equal("p0", rival.Active.CardId);
        Assert.Equal(1, rival.DeckCount);
        Assert.Equal(2, rival.HandCount); // solo el conteo, nunca el contenido
    }
}
