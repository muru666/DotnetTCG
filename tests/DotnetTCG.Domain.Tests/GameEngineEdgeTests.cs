using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Engine;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Tests;

public sealed class GameEngineEdgeTests
{
    private static GameEngine NewEngine()
        => new(GameEngine.CreateDefault(new Random(13),
            new SeededCoinFlip(Enumerable.Repeat(true, 5000).ToArray())));

    private static GameState NewState(int? prizes = 6)
    {
        var state = new GameState(new PlayerBoard(), new PlayerBoard(), 0, suddenDeath: false);
        state.SetStatus(GameStatus.InProgress);
        foreach (var board in state.Players)
        {
            board.LoadDeckForTests(TestHelpers.DeckWithBasics(60));
            var active = new PokemonInPlay(TestCards.Attacker("a", "A"));
            active.AttachEnergy(TestCards.BasicEnergy("ae", PokemonType.Colorless));
            board.PlaceActive(active);
            if (prizes is not null)
                board.InitPrizeCards(prizes.Value);
        }

        state.SetPhase(TurnPhase.Main);
        return state;
    }

    [Fact]
    public void SuddenDeath_WhenBothPrizePilesEmptyAfterAction()
    {
        var state = NewState(prizes: 0);
        var engine = NewEngine();
        state.AdvanceTurn();
        state.SetCurrentPlayer(0);
        state.SetPhase(TurnPhase.Main);

        var result = engine.Execute(state, new GameAction(GameActionType.Attack, 0,
            Attack: new Attack("Tackle", "", "0", [PokemonType.Colorless])));

        Assert.True(result.Success, result.Error);
        Assert.Equal(GameStatus.Setup, state.Status);
        Assert.True(state.SuddenDeath);
        Assert.Equal(SetupService.InitialHandSize, state.Players[0].Hand.Count);
        Assert.False(state.SetupConfirmed[0]);
    }

    [Fact]
    public void DeckOut_OnMainPhaseAction_FinishesGame()
    {
        var state = NewState();
        var engine = NewEngine();
        // Mazo vacío fuera del primer turno: la siguiente acción roba → pierde.
        while (state.Players[0].DeckCount > 0)
            state.Players[0].DrawCard();
        state.AdvanceTurn();
        state.SetCurrentPlayer(0);
        state.SetPhase(TurnPhase.Draw);

        var result = engine.Execute(state, new GameAction(GameActionType.Evolve, 0));

        Assert.False(result.Success);
        Assert.Equal(GameStatus.Finished, state.Status);
        // El jugador que debía robar pierde: gana el rival (índice 1).
        Assert.Equal(1, state.WinnerPlayerIndex);
    }

    [Fact]
    public void Attack_WithoutAttackProperty_Rejected()
    {
        var state = NewState();
        var engine = NewEngine();
        state.AdvanceTurn();
        state.SetCurrentPlayer(0);
        state.SetPhase(TurnPhase.Main);

        var result = engine.Execute(state, new GameAction(GameActionType.Attack, 0));

        Assert.False(result.Success);
        Assert.Equal("Attack.CardRequired", result.Error);
    }

    [Fact]
    public void Attack_UnknownAttackName_Rejected()
    {
        var state = NewState();
        var engine = NewEngine();
        state.AdvanceTurn();
        state.SetCurrentPlayer(0);
        state.SetPhase(TurnPhase.Main);

        var result = engine.Execute(state, new GameAction(GameActionType.Attack, 0,
            Attack: new Attack("Not on this card", "", "30", [PokemonType.Colorless])));

        Assert.False(result.Success);
        Assert.Equal("Attack.Unknown", result.Error);
    }

    [Fact]
    public void Ability_WithoutTarget_Rejected()
    {
        var state = NewState();
        var engine = NewEngine();

        var result = engine.Execute(state, new GameAction(GameActionType.UseAbility, 0,
            AbilityName: "Mystical Fire"));

        Assert.False(result.Success);
        Assert.Equal("Ability.TargetRequired", result.Error);
    }

    [Fact]
    public void ChoiceResolution_WithPendingChoice_InvalidId_Unknown()
    {
        var state = NewState();
        var engine = NewEngine();
        state.SetPendingChoice(new PendingChoice("trainer:Evosoda:evosoda", 0, null, "x",
            [new ChoiceOption("0", "evo")], "Evosoda"));
        state.Players[0].AddToHand(TestCards.Item("evosoda", "Evosoda"));

        var result = engine.Execute(state, new GameAction(GameActionType.PlayCard, 0,
            ChoiceId: "different-id"));

        Assert.False(result.Success);
        Assert.Equal("Choice.Pending", result.Error);
    }

    [Fact]
    public void PlayerBoard_TakePrize_WithoutPrizes_Fails()
    {
        var board = new PlayerBoard();

        var result = board.TakePrize();

        Assert.True(result.IsFailure);
        Assert.Equal("Board.NoPrizes", result.Error.Code);
    }

    [Fact]
    public void PlayerBoard_DrawFromEmpty_Fails()
    {
        var board = new PlayerBoard();

        var result = board.DrawCard();

        Assert.Equal("Board.DeckEmpty", result.Error.Code);
    }

    [Fact]
    public void PlayerBoard_RemoveFromDeck_Missing_ReturnsNull()
    {
        var board = new PlayerBoard();
        board.LoadDeckForTests([TestCards.BasicEnergy("e", PokemonType.Fire)]);

        Assert.Null(board.RemoveFromDeck(c => c.Name == "Nope"));
        Assert.Equal(1, board.DeckCount);
    }

    [Fact]
    public void PlayerBoard_PeekTop_Empty_ReturnsNull()
    {
        Assert.Null(new PlayerBoard().PeekTop());
    }

    [Fact]
    public void PlayerBoard_RecycleFirst_EmptyDiscard_ReturnsNull()
    {
        Assert.Null(new PlayerBoard().RecycleFirst());
    }

    [Fact]
    public void PlayerBoard_ShuffleDeck_PreservesCards()
    {
        var board = new PlayerBoard();
        var cards = Enumerable.Range(0, 10)
            .Select(i => TestCards.BasicEnergy($"e{i}", PokemonType.Fire)).ToList();
        board.LoadDeckForTests(cards);

        board.ShuffleDeck(new Random(1));

        Assert.Equal(10, board.DeckCount);
        Assert.Equal(10, board.DeckCards.Distinct().Count());
    }

    [Fact]
    public void PlayerBoard_MillTop_WithoutDeck_Stops()
    {
        var board = new PlayerBoard();

        board.MillTop(5);

        Assert.Empty(board.DiscardPile);
    }
}
