using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Engine;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Tests;

public sealed class GameEngineActionTests
{
    private static GameEngine NewEngine()
        => new(GameEngine.CreateDefault(new Random(11),
            new SeededCoinFlip(Enumerable.Repeat(true, 2000).ToArray())));

    private static GameState NewState()
    {
        var state = new GameState(new PlayerBoard(), new PlayerBoard(), 0, suddenDeath: false);
        state.SetStatus(GameStatus.InProgress);
        foreach (var board in state.Players)
        {
            board.LoadDeckForTests(TestHelpers.DeckWithBasics(60));
            board.PlaceActive(new PokemonInPlay(TestCards.BasicPokemon("a", "A", hp: 100)));
            board.InitPrizeCards(6);
        }

        state.SetPhase(TurnPhase.Main);
        return state;
    }

    [Fact]
    public void Concede_OpponentWins_Anytime()
    {
        var state = NewState();
        var engine = NewEngine();
        // Concede funciona aunque no sea el turno del que rinde.
        state.SetCurrentPlayer(1);

        var result = engine.Execute(state, new GameAction(GameActionType.Concede, 0));

        Assert.True(result.Success, result.Error);
        Assert.Equal(GameStatus.Finished, state.Status);
        Assert.Equal(1, state.WinnerPlayerIndex);
    }

    [Fact]
    public void PassTurn_NotYourTurn_Fails()
    {
        var state = NewState();
        var engine = NewEngine();

        var result = engine.Execute(state, new GameAction(GameActionType.Attack, 1,
            Attack: new Attack("Tackle", "", "30", [PokemonType.Colorless])));

        Assert.False(result.Success);
        Assert.Equal("Turn.NotYourTurn", result.Error);
    }

    [Fact]
    public void ActionOnFinishedGame_Fails()
    {
        var state = NewState();
        var engine = NewEngine();
        state.Finish(1);

        var result = engine.Execute(state, new GameAction(GameActionType.PassTurn, 0));

        Assert.Equal("Game.Finished", result.Error);
    }

    [Fact]
    public void Evolve_Mega_EndsTurnImmediately()
    {
        var state = NewState();
        var engine = NewEngine();
        var stage1 = TestCards.Stage1Pokemon("s1", "Base", "A", hp: 90) with
        {
            Subtypes = [SubType.Stage1],
        };
        // target es "A" (Basic) — evolucionamos con Mega que evoluciona desde "A".
        var mega = TestCards.Stage1Pokemon("mega", "MegaMon", "A", hp: 200) with
        {
            Subtypes = [SubType.Mega],
        };
        var target = state.Players[0].Active!;
        target.JustPlayed = false;
        state.Players[0].AddToHand(mega);
        _ = stage1;
        // Fuera del primer turno del jugador inicial (regla de evolución).
        state.AdvanceTurn();
        state.SetCurrentPlayer(0);
        state.SetPhase(TurnPhase.Main);
        var turnBefore = state.TurnNumber;

        var result = engine.Execute(state, new GameAction(GameActionType.Evolve, 0,
            Card: mega, Target: target));

        Assert.True(result.Success, result.Error);
        Assert.Equal("MegaMon", target.Card.Name);
        Assert.Equal(turnBefore + 1, state.TurnNumber); // MEGA termina el turno
        Assert.Equal(1, state.CurrentPlayerIndex);
    }

    [Fact]
    public void Evolve_NoTarget_Rejected()
    {
        var state = NewState();
        var engine = NewEngine();
        var mega = TestCards.Stage1Pokemon("mega", "MegaMon", "A", hp: 200);
        state.Players[0].AddToHand(mega);

        var result = engine.Execute(state, new GameAction(GameActionType.Evolve, 0, Card: mega));

        Assert.Equal("Evolve.TargetRequired", result.Error);
    }

    [Fact]
    public void AttachEnergy_ForeignTarget_Rejected()
    {
        var state = NewState();
        var engine = NewEngine();
        var energy = TestCards.BasicEnergy("e", PokemonType.Fire);
        state.Players[0].AddToHand(energy);

        var result = engine.Execute(state, new GameAction(GameActionType.AttachEnergy, 0,
            Card: energy, Target: state.Players[1].Active));

        Assert.Equal("Energy.ForeignTarget", result.Error);
    }

    [Fact]
    public void AttachEnergy_Rainbow_Deals10ToSelf()
    {
        var state = NewState();
        var engine = NewEngine();
        var rainbow = new EnergyCard("rb", "Rainbow Energy", [], [], "xy1", "",
            PokemonType.Colorless, SpecialEnergyType.Rainbow);
        state.Players[0].AddToHand(rainbow);

        var result = engine.Execute(state, new GameAction(GameActionType.AttachEnergy, 0,
            Card: rainbow, Target: state.Players[0].Active));

        Assert.True(result.Success, result.Error);
        Assert.Equal(10, state.Players[0].Active!.TotalDamage);
    }

    [Fact]
    public void AttachEnergy_TwicePerTurn_Blocked()
    {
        var state = NewState();
        var engine = NewEngine();
        var first = TestCards.BasicEnergy("e1", PokemonType.Fire);
        var second = TestCards.BasicEnergy("e2", PokemonType.Water);
        state.Players[0].AddToHand(first);
        state.Players[0].AddToHand(second);

        engine.Execute(state, new GameAction(GameActionType.AttachEnergy, 0,
            Card: first, Target: state.Players[0].Active));
        var result = engine.Execute(state, new GameAction(GameActionType.AttachEnergy, 0,
            Card: second, Target: state.Players[0].Active));

        Assert.Equal("Energy.OncePerTurn", result.Error);
    }

    [Fact]
    public void PlayPokemon_NotBasic_Rejected()
    {
        var state = NewState();
        var engine = NewEngine();
        var stage1 = TestCards.Stage1Pokemon("s", "Evo", "A", hp: 90);
        state.Players[0].AddToHand(stage1);

        var result = engine.Execute(state, new GameAction(GameActionType.PlayCard, 0, Card: stage1));

        Assert.Equal("Play.NotBasic", result.Error);
    }

    [Fact]
    public void ActionTargetNotInPlay_Rejected()
    {
        var state = NewState();
        var engine = NewEngine();

        var result = engine.Execute(state, new GameAction(GameActionType.Retreat, 0,
            Target: new PokemonInPlay(TestCards.BasicPokemon("x", "X", hp: 60))));

        // Target es ajeno al campo → CanRetreat o handler decide.
        Assert.False(result.Success);
    }

    [Fact]
    public void WaterShuriken_PendingChoiceFlow()
    {
        var state = NewState();
        var engine = NewEngine();
        var userCard = TestCards.BasicPokemon("ws", "Kyogre", hp: 100) with
        {
            Abilities = [new PokemonAbility("Water Shuriken", "30 to a benched Pokemon", AbilityType.Activated)],
        };
        state.Players[0].AddToBench(new PokemonInPlay(userCard));
        state.Players[1].AddToBench(new PokemonInPlay(TestCards.BasicPokemon("ob", "OppBench", hp: 60)));

        var result = engine.Execute(state, new GameAction(GameActionType.UseAbility, 0,
            Target: state.Players[0].Bench[0], AbilityName: "Water Shuriken"));

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.PendingChoice);

        var resume = engine.Execute(state, new GameAction(GameActionType.UseAbility, 0,
            ChoiceId: result.PendingChoice!.ChoiceId, SelectedOptionIds: ["bench:0"]));

        Assert.True(resume.Success, resume.Error);
        Assert.Null(state.PendingChoice);
        Assert.Equal(1, state.AbilitiesUsedThisTurn);
    }

    [Fact]
    public void WaterShuriken_WithWaterEnergy_Deals30()
    {
        var state = NewState();
        var engine = NewEngine();
        var userCard = TestCards.BasicPokemon("ws", "Kyogre", hp: 100) with
        {
            Abilities = [new PokemonAbility("Water Shuriken", "text", AbilityType.Activated)],
        };
        state.Players[0].AddToBench(new PokemonInPlay(userCard));
        state.Players[0].AddToHand(TestCards.BasicEnergy("water", PokemonType.Water));

        var result = engine.Execute(state, new GameAction(GameActionType.UseAbility, 0,
            Target: state.Players[0].Bench[0], AbilityName: "Water Shuriken",
            SelectedOptionIds: ["active"]));

        Assert.True(result.Success, result.Error);
        Assert.Equal(30, state.Players[1].Active!.TotalDamage);
        Assert.Contains(state.Players[0].DiscardPile, c => c.Id == "water");
    }

    [Fact]
    public void Ability_UnknownName_Rejected()
    {
        var state = NewState();
        var engine = NewEngine();

        var result = engine.Execute(state, new GameAction(GameActionType.UseAbility, 0,
            Target: state.Players[0].Active, AbilityName: "Nope"));

        Assert.Equal("Ability.Missing", result.Error);
    }

    [Fact]
    public void SecondAbilityPerTurn_Blocked()
    {
        var state = NewState();
        var engine = NewEngine();
        var card = TestCards.BasicPokemon("ab", "User", hp: 100) with
        {
            Abilities = [new PokemonAbility("Mystical Fire", "text", AbilityType.Activated)],
        };
        state.Players[0].AddToBench(new PokemonInPlay(card));

        var first = engine.Execute(state, new GameAction(GameActionType.UseAbility, 0,
            Target: state.Players[0].Bench[0], AbilityName: "Mystical Fire"));
        var second = engine.Execute(state, new GameAction(GameActionType.UseAbility, 0,
            Target: state.Players[0].Bench[0], AbilityName: "Mystical Fire"));

        Assert.True(first.Success, first.Error);
        Assert.Equal("Ability.OncePerTurn", second.Error);
    }

    [Fact]
    public void Retreat_SwapsAndCures()
    {
        var state = NewState();
        var engine = NewEngine();
        var active = state.Players[0].Active!;
        active.AttachEnergy(TestCards.BasicEnergy("e", PokemonType.Fire));
        active.AttachEnergy(TestCards.BasicEnergy("e2", PokemonType.Water));
        active.AddStatus(Status.Confused); // permite retirarse (Paralyzed/Asleep no)
        var bench = new PokemonInPlay(TestCards.BasicPokemon("b", "B", hp: 60));
        state.Players[0].AddToBench(bench);

        var result = engine.Execute(state, new GameAction(GameActionType.Retreat, 0,
            Target: bench,
            EnergiesToDiscard: [active.AttachedEnergies[0]]));

        Assert.True(result.Success, result.Error);
        Assert.Same(bench, state.Players[0].Active);
        Assert.Empty(active.Statuses);
        Assert.Single(active.AttachedEnergies);
        Assert.Contains(state.Players[0].DiscardPile, c => c.Id == "e");
        Assert.Equal(1, state.RetreatsThisTurn);
    }

    [Fact]
    public void ManualDraw_FromDrawPhase_GoesToMain()
    {
        var state = NewState();
        var engine = NewEngine();
        // Fuera del primer turno del jugador inicial (ese no roba carta).
        state.AdvanceTurn();
        state.SetCurrentPlayer(0);
        state.SetPhase(TurnPhase.Draw);

        var result = engine.Execute(state, new GameAction(GameActionType.DrawCard, 0));

        Assert.True(result.Success, result.Error);
        Assert.Equal(TurnPhase.Main, state.Phase);
        Assert.Single(state.Players[0].Hand);
    }

    [Fact]
    public void ManualDraw_FromMain_Rejected()
    {
        var state = NewState();
        var engine = NewEngine();

        var result = engine.Execute(state, new GameAction(GameActionType.DrawCard, 0));

        Assert.Equal("Draw.NotDrawPhase", result.Error);
    }

    [Fact]
    public void UnknownActionType_Rejected()
    {
        var state = NewState();
        var engine = NewEngine();

        var result = engine.Execute(state, new GameAction(GameActionType.DrawCard, 0) with
        {
            Type = (GameActionType)999,
        });

        Assert.Equal("Action.Unknown", result.Error);
    }
}
