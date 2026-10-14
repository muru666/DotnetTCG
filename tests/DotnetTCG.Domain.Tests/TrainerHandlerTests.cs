using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Engine;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Tests;

public sealed class TrainerHandlerTests
{
    private static GameEngine NewEngine()
        => new(GameEngine.CreateDefault(new Random(9),
            new SeededCoinFlip(Enumerable.Repeat(true, 1000).ToArray())));

    private static GameState NewState(params Card[] hand)
    {
        var state = new GameState(new PlayerBoard(), new PlayerBoard(), 0, suddenDeath: false);
        state.SetStatus(GameStatus.InProgress);
        foreach (var board in state.Players)
        {
            board.LoadDeckForTests(TestHelpers.DeckWithBasics(60));
            board.PlaceActive(new PokemonInPlay(TestCards.BasicPokemon("a", "A", hp: 100)));
        }

        foreach (var card in hand)
            state.Players[0].AddToHand(card);
        // Un estado InProgress real tiene premios: sin ellas CheckVictory
        // interpretaría 0-0 como sudden death (regla correcta, pero no aquí).
        state.Players[0].InitPrizeCards(6);
        state.Players[1].InitPrizeCards(6);
        state.SetPhase(TurnPhase.Main);
        return state;
    }

    private static GameActionResult Play(GameState state, GameEngine engine, Card card,
        GameActionType type = GameActionType.PlayCard, PokemonInPlay? target = null)
        => engine.Execute(state, new GameAction(type, 0, Card: card, Target: target));

    [Fact]
    public void PlayStadium_ReplacesPreviousStadium()
    {
        var state = NewState();
        var engine = NewEngine();
        var first = TestCards.Stadium("st1", "Fairy Garden");
        var second = TestCards.Stadium("st2", "Shadow Circle");
        state.Players[0].AddToHand(first);
        state.Players[0].AddToHand(second);

        var r0 = Play(state, engine, first, GameActionType.PlayStadium);
        Assert.True(r0.Success, r0.Error);

        // Un estadio por turno: el segundo se juega en el turno siguiente
        // y reemplaza al anterior (la carta vieja va al descarte).
        state.AdvanceTurn();
        state.SetCurrentPlayer(0);
        state.SetPhase(TurnPhase.Main);
        var secondResult = Play(state, engine, second, GameActionType.PlayStadium);

        Assert.True(secondResult.Success, secondResult.Error);
        Assert.Equal("st2", state.StadiumCard!.Id);
        Assert.Contains(state.Players[0].DiscardPile, c => c.Id == "st1");
        Assert.Equal(1, state.StadiumsPlayedThisTurn);
    }

    [Fact]
    public void PlayStadium_SecondPerTurn_Blocked()
    {
        var state = NewState();
        var engine = NewEngine();
        var a = TestCards.Stadium("st1", "A");
        var b = TestCards.Stadium("st2", "B");
        state.Players[0].AddToHand(a);
        state.Players[0].AddToHand(b);

        Play(state, engine, a, GameActionType.PlayStadium);
        var result = Play(state, engine, b, GameActionType.PlayStadium);

        Assert.False(result.Success);
        Assert.Equal("Trainer.StadiumOnce", result.Error);
    }

    [Fact]
    public void PlayTool_AttachesAndAppliesModifier()
    {
        var state = NewState();
        var engine = NewEngine();
        var band = TestCards.Tool("tool", "Muscle Band");
        state.Players[0].AddToHand(band);
        var active = state.Players[0].Active!;

        var result = Play(state, engine, band, target: active);

        Assert.True(result.Success, result.Error);
        Assert.Same(band, active.AttachedTool);
        Assert.True(active.HasModifier(ModifierType.DamageBoost, state.TurnNumber));

        var damage = new DamageCalculator().Calculate(new DamageCalculationInput(
            30, "Tackle", active,
            state.Players[1].Active!, null));
        Assert.Equal(50, damage);
    }

    [Fact]
    public void PlayTool_OnOccupiedTarget_Blocked()
    {
        var state = NewState();
        var engine = NewEngine();
        var first = TestCards.Tool("t1", "Tool 1");
        var second = TestCards.Tool("t2", "Tool 2");
        state.Players[0].AddToHand(first);
        state.Players[0].AddToHand(second);
        var active = state.Players[0].Active!;
        active.AttachTool(first);

        var result = Play(state, engine, second, target: active);

        Assert.Equal("Trainer.ToolOccupied", result.Error);
    }

    [Fact]
    public void PlayPotion_Heals30()
    {
        var state = NewState();
        var engine = NewEngine();
        var potion = TestCards.Item("pot", "Potion");
        state.Players[0].AddToHand(potion);
        state.Players[0].Active!.AddDamage(50);

        var result = Play(state, engine, potion, target: state.Players[0].Active);

        Assert.True(result.Success, result.Error);
        Assert.Equal(20, state.Players[0].Active!.TotalDamage);
        Assert.Contains(state.Players[0].DiscardPile, c => c.Id == "pot");
    }

    [Fact]
    public void PlayTierno_DrawsThree()
    {
        var state = NewState();
        var engine = NewEngine();
        var tierno = TestCards.Supporter("s", "Tierno");
        state.Players[0].AddToHand(tierno);

        var result = Play(state, engine, tierno, GameActionType.PlaySupporter);

        Assert.True(result.Success, result.Error);
        Assert.Equal(3, state.Players[0].Hand.Count);
        Assert.Contains(state.Players[0].DiscardPile, c => c.Id == "s");
        Assert.Equal(1, state.SupportersPlayedThisTurn);
    }

    [Fact]
    public void PlaySycamore_DiscardsHandAndDrawsSeven()
    {
        var state = NewState();
        var engine = NewEngine();
        var sycamore = TestCards.Supporter("prof", "Professor Sycamore");
        state.Players[0].AddToHand(sycamore);
        state.Players[0].AddToHand(TestCards.BasicEnergy("extra", PokemonType.Fire));

        var result = Play(state, engine, sycamore, GameActionType.PlaySupporter);

        Assert.True(result.Success, result.Error);
        Assert.Equal(7, state.Players[0].Hand.Count);
        Assert.Contains(state.Players[0].DiscardPile, c => c.Id == "prof");
    }

    [Fact]
    public void PlaySecondSupporter_Blocked()
    {
        var state = NewState();
        var engine = NewEngine();
        var first = TestCards.Supporter("s1", "Tierno");
        var second = TestCards.Supporter("s2", "Shauna");
        state.Players[0].AddToHand(first);
        state.Players[0].AddToHand(second);

        Play(state, engine, first, GameActionType.PlaySupporter);
        var result = Play(state, engine, second, GameActionType.PlaySupporter);

        Assert.Equal("Trainer.SupporterOnce", result.Error);
    }

    [Fact]
    public void TrainerNotInHand_Blocked()
    {
        var state = NewState();
        var engine = NewEngine();
        var potion = TestCards.Item("pot", "Potion"); // no está en mano

        var result = Play(state, engine, potion);

        Assert.False(result.Success);
        Assert.Equal("Trainer.NotInHand", result.Error);
    }

    [Fact]
    public void EvosodaChoice_FindsEvoInDeckAndMovesToHand()
    {
        var state = NewState();
        var engine = NewEngine();
        var evo = TestCards.Stage1Pokemon("evo", "Raichu", "A", hp: 90);
        var deck = state.Players[0].DeckCards.ToList();
        state.Players[0].LoadDeckForTests([evo, .. deck]);
        var evoCard = TestCards.Item("evosoda", "Evosoda");
        state.Players[0].AddToHand(evoCard);

        var play = Play(state, engine, evoCard);

        Assert.True(play.Success, play.Error);
        Assert.NotNull(play.PendingChoice);
        Assert.Single(play.PendingChoice!.Options);

        var resolve = engine.Execute(state, new GameAction(GameActionType.PlayCard, 0,
            ChoiceId: play.PendingChoice.ChoiceId, SelectedOptionIds: ["0"]));

        Assert.True(resolve.Success, resolve.Error);
        Assert.Contains(state.Players[0].Hand, c => c.Id == "evo");
        Assert.Contains(state.Players[0].DiscardPile, c => c.Id == "evosoda");
    }

    [Fact]
    public void EvosodaChoice_InvalidIndex_Rejected()
    {
        var state = NewState();
        var engine = NewEngine();
        var evo = TestCards.Stage1Pokemon("evo", "Raichu", "A", hp: 90);
        var deck = state.Players[0].DeckCards.ToList();
        state.Players[0].LoadDeckForTests([evo, .. deck]);
        var evoCard = TestCards.Item("evosoda", "Evosoda");
        state.Players[0].AddToHand(evoCard);
        var play = Play(state, engine, evoCard);

        var resolve = engine.Execute(state, new GameAction(GameActionType.PlayCard, 0,
            ChoiceId: play.PendingChoice!.ChoiceId, SelectedOptionIds: ["9"]));

        Assert.False(resolve.Success);
        Assert.Equal("Choice.InvalidOption", resolve.Error);
    }

    [Fact]
    public void EvosodaChoice_WrongChoiceId_Rejected()
    {
        var state = NewState();
        var engine = NewEngine();
        var evoCard = TestCards.Item("evosoda", "Evosoda");
        state.Players[0].AddToHand(evoCard);
        state.SetPendingChoice(new PendingChoice("other", 0, null, "?", []));

        var resolve = engine.Execute(state, new GameAction(GameActionType.PlayCard, 0,
            ChoiceId: "other"));

        Assert.False(resolve.Success);
    }

    [Fact]
    public void CassiusChoice_ReturnsActiveToHandAndPromotesBench()
    {
        var state = NewState();
        var engine = NewEngine();
        var bench = new PokemonInPlay(TestCards.BasicPokemon("b", "B", hp: 60));
        state.Players[0].AddToBench(bench);
        var cassius = TestCards.Supporter("cas", "Cassius");
        state.Players[0].AddToHand(cassius);

        var play = Play(state, engine, cassius, GameActionType.PlaySupporter);

        Assert.True(play.Success, play.Error);
        Assert.NotNull(play.PendingChoice);

        var resolve = engine.Execute(state, new GameAction(GameActionType.PlayCard, 0,
            ChoiceId: play.PendingChoice!.ChoiceId, SelectedOptionIds: ["0"]));

        Assert.True(resolve.Success, resolve.Error);
        Assert.Same(bench, state.Players[0].Active);
        Assert.Contains(state.Players[0].Hand, c => c.Id == "a");
        Assert.Contains(state.Players[0].DiscardPile, c => c.Id == "cas");
    }

    [Fact]
    public void MaxReviveChoice_MovesBasicFromDiscardToBench()
    {
        var state = NewState();
        var engine = NewEngine();
        var dead = TestCards.BasicPokemon("dead", "Revived", hp: 60);
        state.Players[0].Discard(dead);
        var maxRevive = TestCards.Item("maxrev", "Max Revive");
        state.Players[0].AddToHand(maxRevive);

        var play = Play(state, engine, maxRevive);
        Assert.True(play.Success, play.Error);
        Assert.NotNull(play.PendingChoice);

        var resolve = engine.Execute(state, new GameAction(GameActionType.PlayCard, 0,
            ChoiceId: play.PendingChoice!.ChoiceId, SelectedOptionIds: ["0"]));

        Assert.True(resolve.Success, resolve.Error);
        Assert.Contains(state.Players[0].Bench, p => p.Card.Id == "dead");
        Assert.Contains(state.Players[0].DiscardPile, c => c.Id == "maxrev");
    }

    [Fact]
    public void GreatBallChoice_NoPokemonInDeck_NoChoicePending()
    {
        var state = NewState();
        var engine = NewEngine();
        var greatBall = TestCards.Item("gb", "Great Ball");
        state.Players[0].AddToHand(greatBall);
        // mazo: básicos + energías; hay pokemon → sí encuentra. Vaciamos pokemon del mazo:
        var noPokemon = Enumerable.Range(0, 59)
            .Select(i => TestCards.BasicEnergy($"e{i}", PokemonType.Colorless)).ToList();
        state.Players[0].LoadDeckForTests(noPokemon);

        var play = Play(state, engine, greatBall);

        Assert.False(play.Success);
        Assert.Equal("Trainer.NoChoice", play.Error);
    }
}
