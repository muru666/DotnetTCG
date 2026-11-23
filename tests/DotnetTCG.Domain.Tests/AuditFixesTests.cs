using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Engine;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Tests;

/// <summary>
/// Tests de regresión de la auditoría 2026-11 (docs/AUDIT-2026-11.md):
/// BUG-D001..D006 (los hallazgos High de Dominio).
/// </summary>
public sealed class AuditFixesTests
{
    // ---------------------------------------------------------- helpers
    private static GameEngine SeededEngine(bool heads = true)
        => new(GameEngine.CreateDefault(new Random(1), new SeededCoinFlip(heads)));

    private static GameState NewState(IReadOnlyList<Attack>? attacks0 = null)
    {
        var state = new GameState(new PlayerBoard(), new PlayerBoard(), 0, suddenDeath: false);
        state.SetStatus(GameStatus.InProgress);
        var seq = 0;
        foreach (var board in state.Players)
        {
            board.LoadDeckForTests(TestHelpers.DeckWithBasics(60));
            board.PlaceActive(new PokemonInPlay(TestCards.BasicPokemon($"p{seq++}", "P", 200, attacks: attacks0)));
            board.InitPrizeCards(6);
        }

        // TurnNumber 2: salta la restricción de primer turno (CanAttack/CanGoToAttack).
        new TurnManager().EndTurn(state);
        state.SetCurrentPlayer(0);
        state.SetPhase(TurnPhase.Main);
        return state;
    }

    private static Attack ElectronCrush()
        => new("Electron Crush", "", "50", [PokemonType.Lightning, PokemonType.Lightning]);

    private static GameState ElectronCrushState()
    {
        var state = NewState([ElectronCrush()]);
        state.Players[0].Active!.AttachEnergy(TestCards.BasicEnergy("l1", PokemonType.Lightning));
        state.Players[0].Active.AttachEnergy(TestCards.BasicEnergy("l2", PokemonType.Lightning));
        return state;
    }

    private static PendingChoice StartElectronCrush(GameState state, GameEngine engine, out GameActionResult first)
    {
        first = engine.Execute(state, new GameAction(GameActionType.Attack, 0, Attack: ElectronCrush()));
        Assert.True(first.Success, first.Error);
        Assert.NotNull(first.PendingChoice);
        return first.PendingChoice!;
    }

    // ---------------------------------------------------------- BUG-D001
    [Fact]
    public void Attack_Resume_RecomputesBaseAndVariableDamage()
    {
        var state = ElectronCrushState();
        var engine = SeededEngine();
        var pending = StartElectronCrush(state, engine, out _);

        var resume = engine.Execute(state, new GameAction(GameActionType.Attack, 0,
            Attack: ElectronCrush(), ChoiceId: pending.ChoiceId, SelectedOptionIds: ["yes"]));

        Assert.True(resume.Success, resume.Error);
        // 50 impresos + 30 por descartar la energía.
        Assert.Equal(80, state.Players[1].Active!.TotalDamage);
        // ...y la energía se descartó igual.
        Assert.Equal(1, state.Players[0].Active!.AttachedEnergies.Count);
    }

    // ---------------------------------------------------------- BUG-D002
    [Fact]
    public void PendingChoice_OtherPlayer_CannotResolveIt()
    {
        var state = ElectronCrushState();
        var engine = SeededEngine();
        var pending = StartElectronCrush(state, engine, out _);

        var rival = engine.Execute(state, new GameAction(GameActionType.Attack, 1,
            ChoiceId: pending.ChoiceId, SelectedOptionIds: ["no"]));

        Assert.False(rival.Success);
        Assert.Equal("Choice.NotYours", rival.Error);
        Assert.NotNull(state.PendingChoice);
        Assert.Equal(0, state.Players[1].Active!.TotalDamage);
    }

    // ---------------------------------------------------------- BUG-D003
    [Fact]
    public void UntilNextTurn_CoversOpponentNextTurn()
    {
        var pokemon = new PokemonInPlay(TestCards.BasicPokemon("x", "X", 100));
        pokemon.AddModifier(new BoardModifier(
            ModifierType.CannotAttack, "Torment", 0, ModifierDuration.UntilNextTurn, TurnApplied: 2));

        Assert.True(pokemon.HasModifier(ModifierType.CannotAttack, 2));
        // El turno del rival: es exactamente la ventana que debe cubrir.
        Assert.True(pokemon.HasModifier(ModifierType.CannotAttack, 3));

        pokemon.RemoveExpiredModifiers(3); // fin del turno 3 (TurnNumber ya avanzó a 4)
        Assert.False(pokemon.HasModifier(ModifierType.CannotAttack, 4));
    }

    [Fact]
    public void UntilNextTurn_BlocksAttackOnOpponentTurn_ViaRuleValidator()
    {
        var state = NewState();
        state.Players[1].Active!.AddModifier(new BoardModifier(
            ModifierType.CannotAttack, "Torment", 0, ModifierDuration.UntilNextTurn,
            TurnApplied: state.TurnNumber));

        var err = new RuleValidator().CanAttack(state, 1,
            new Attack("Tackle", "", "30", [PokemonType.Colorless]));

        Assert.Equal("Attack.Blocked", err?.Code);
    }

    // ---------------------------------------------------------- BUG-D004
    [Fact]
    public void CanAttack_WrongEnergyType_IsRejected()
    {
        var state = NewState();
        state.SetCurrentPlayer(1);
        var active = state.Players[1].Active!;
        active.AttachEnergy(TestCards.BasicEnergy("w1", PokemonType.Water));
        active.AttachEnergy(TestCards.BasicEnergy("w2", PokemonType.Water));

        var err = new RuleValidator().CanAttack(state, 1,
            new Attack("Flamethrower", "", "60", [PokemonType.Fire, PokemonType.Fire]));

        Assert.Equal("Attack.EnergyShort", err?.Code);
    }

    [Fact]
    public void CanAttack_ColorlessCost_PaidWithAnyEnergy()
    {
        var state = NewState();
        state.SetCurrentPlayer(1);
        var active = state.Players[1].Active!;
        active.AttachEnergy(TestCards.BasicEnergy("w1", PokemonType.Water));

        var err = new RuleValidator().CanAttack(state, 1,
            new Attack("Tackle", "", "30", [PokemonType.Colorless]));

        Assert.Null(err);
    }

    [Fact]
    public void CanAttack_DoubleColorless_PaysTwoColorlessCost()
    {
        Assert.True(RuleValidator.CanPayCost(
            [PokemonType.Colorless, PokemonType.Colorless],
            [TestCards.DoubleColorless()]));
        Assert.False(RuleValidator.CanPayCost(
            [PokemonType.Fire], [TestCards.DoubleColorless()]));
    }

    // ---------------------------------------------------------- BUG-D005
    [Fact]
    public void FailedAttack_EndsTurn_AndCannotBeRetried()
    {
        var state = NewState([new Attack("Tackle", "", "30", [PokemonType.Colorless])]);
        state.Players[0].Active!.AttachEnergy(TestCards.BasicEnergy("c1", PokemonType.Colorless));
        state.Players[0].Active.AddStatus(Status.Confused);
        var engine = SeededEngine(heads: false); // la tirada de confusión sale siempre cruz

        var failed = engine.Execute(state,
            new GameAction(GameActionType.Attack, 0, Attack: new Attack("Tackle", "", "30", [PokemonType.Colorless])));

        Assert.False(failed.Success);
        Assert.Equal("Attack.ConfusedSelfHit", failed.Error);
        // El turno terminó (ya no es el turno del atacante).
        Assert.NotEqual(TurnPhase.Attack, state.Phase);
        Assert.NotEqual(0, state.CurrentPlayerIndex);

        var retry = engine.Execute(state,
            new GameAction(GameActionType.Attack, 0, Attack: new Attack("Tackle", "", "30", [PokemonType.Colorless])));

        Assert.False(retry.Success);
        Assert.Equal("Turn.NotYourTurn", retry.Error);
        // Un solo intento: el autodaño no se acumuló por reintentar.
        Assert.Equal(30, state.Players[0].Active!.TotalDamage);
    }

    // ---------------------------------------------------------- BUG-D006
    [Fact]
    public void MaxRevive_WithFullBench_RejectsWithoutCreatingPending()
    {
        var maxRevive = new TrainerCard("xy1-mr", "Max Revive", [SubType.Item], [],
            "xy1", string.Empty, SubType.Item);
        var state = NewState();
        var board = state.Players[0];
        board.AddToHand(maxRevive);
        for (var i = 0; i < PlayerBoard.MaxBenchSize; i++)
            board.AddToBench(new PokemonInPlay(TestCards.BasicPokemon($"b{i}", $"B{i}", 60)));
        board.Discard(TestCards.BasicPokemon("grave", "Grave", 60));
        var engine = SeededEngine();

        var result = engine.Execute(state, new GameAction(GameActionType.PlayCard, 0, Card: maxRevive));

        Assert.False(result.Success);
        Assert.Equal("Choice.BenchFull", result.Error);
        Assert.Null(state.PendingChoice);
        // La carta sigue en la mano (no se jugó).
        Assert.Contains(maxRevive, board.Hand);
    }

    [Fact]
    public void Concede_IsAllowedWhilePendingChoiceIsOpen()
    {
        var state = ElectronCrushState();
        var engine = SeededEngine();
        StartElectronCrush(state, engine, out _);

        var concede = engine.Execute(state, new GameAction(GameActionType.Concede, 0));

        Assert.True(concede.Success, concede.Error);
        Assert.Equal(GameStatus.Finished, state.Status);
        Assert.Equal(1, state.WinnerPlayerIndex);
    }
}
