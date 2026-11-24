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

    // ---------------------------------------------------------- BUG-010 (ACE SPEC)
    [Fact]
    public void ValidateDeck_RejectsMoreThanOneAceSpec()
    {
        var deck = new List<Card>();
        for (var i = 0; i < 58; i++)
            deck.Add(TestCards.BasicPokemon($"p{i}", $"P{i}", 60));
        deck.Add(new TrainerCard("xy1-a1", "Ace One", [SubType.AceSpec], [],
            "xy1", string.Empty, SubType.AceSpec));
        deck.Add(new TrainerCard("xy1-a2", "Ace Two", [SubType.AceSpec], [],
            "xy1", string.Empty, SubType.AceSpec));

        var errors = new RuleValidator().ValidateDeck(deck);

        Assert.Contains(errors, e => e.Code == "Deck.AceSpec");
    }

    [Fact]
    public void NewGame_RejectsDecksWithTwoAceSpec()
    {
        // La regla vive en el Dominio: NewGame también la valida (antes solo
        // DeckService, y con el flag IsValid se podía eludir).
        var bad = new List<Card>();
        for (var i = 0; i < 58; i++)
            bad.Add(TestCards.BasicPokemon($"p{i}", $"P{i}", 60));
        bad.Add(new TrainerCard("xy1-a1", "Ace One", [SubType.AceSpec], [],
            "xy1", string.Empty, SubType.AceSpec));
        bad.Add(new TrainerCard("xy1-a2", "Ace Two", [SubType.AceSpec], [],
            "xy1", string.Empty, SubType.AceSpec));
        var good = new List<Card>();
        for (var i = 0; i < 60; i++)
            good.Add(TestCards.BasicPokemon($"g{i}", $"G{i}", 60));

        var engine = SeededEngine();
        var ex = Assert.Throws<InvalidOperationException>(
            () => engine.NewGame(bad, good, 0));

        Assert.Contains("Deck.AceSpec", ex.Message);
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

    // ---------------------------------------------------------- BUG-D007
    [Fact]
    public void RainbowAttach_KoIsResolvedImmediately()
    {
        // Un KO por efecto (sin ataque) debe resolver al toque: promoción + premio.
        var state = NewState();
        var board = state.Players[0];
        board.ClearActive();
        var low = new PokemonInPlay(TestCards.BasicPokemon("low", "Low", 10));
        board.PlaceActive(low);
        board.AddToBench(new PokemonInPlay(TestCards.BasicPokemon("bench0", "Bench0", 60)));
        var rainbow = new EnergyCard("rb", "Rainbow Energy", [], [], "xy1", "",
            PokemonType.Colorless, SpecialEnergyType.Rainbow);
        board.AddToHand(rainbow);
        var engine = SeededEngine();

        var result = engine.Execute(state,
            new GameAction(GameActionType.AttachEnergy, 0, Card: rainbow, Target: low));

        Assert.True(result.Success, result.Error);
        Assert.Contains(GameEvent.Knockout, result.Events!);
        Assert.Contains(GameEvent.PrizeTaken, result.Events!);
        Assert.Equal("bench0", state.Players[0].Active!.Card.Id);
        Assert.Equal(5, state.Players[1].PrizeCount);
    }

    [Fact]
    public void PendingAttack_Ko_ResolvesPromotionPrizeAndDestinyBurst()
    {
        var state = ElectronCrushState();
        // Defensor con 40 HP y Destiny Burst; banca en ambos lados.
        var p1 = state.Players[1];
        p1.ClearActive();
        var defender = new PokemonInPlay(new PokemonCard(
            "d40", "Fragile", [SubType.Basic], [], "xy1", "", 40, 1, null,
            PokemonType.Colorless, [],
            [new PokemonAbility("Destiny Burst", "KO back.", AbilityType.Triggered)],
            [], []));
        p1.PlaceActive(defender);
        p1.AddToBench(new PokemonInPlay(TestCards.BasicPokemon("pb1", "PB1", 60)));
        state.Players[0].AddToBench(new PokemonInPlay(TestCards.BasicPokemon("pb0", "PB0", 60)));
        var engine = SeededEngine();
        var pending = StartElectronCrush(state, engine, out _);

        var resume = engine.Execute(state, new GameAction(GameActionType.Attack, 0,
            Attack: ElectronCrush(), ChoiceId: pending.ChoiceId, SelectedOptionIds: ["yes"]));

        Assert.True(resume.Success, resume.Error);
        Assert.Contains(GameEvent.Knockout, resume.Events!);
        // Defensor promovido (el KO del ataque con pendiente se resolvió)...
        Assert.Equal("pb1", state.Players[1].Active!.Card.Id);
        // ...y Destiny Burst KOó al atacante: ambos tomaron premio (5/5).
        Assert.Equal(5, state.Players[0].PrizeCount);
        Assert.Equal(5, state.Players[1].PrizeCount);
        Assert.Equal("pb0", state.Players[0].Active!.Card.Id);
    }

    // ---------------------------------------------------------- BUG-D008
    [Fact]
    public void DrawCard_WithEmptyDeck_FinishesGame()
    {
        var state = NewState();
        var board = state.Players[0];
        board.Reset();
        board.PlaceActive(new PokemonInPlay(TestCards.BasicPokemon("solo", "Solo", 60)));
        state.SetPhase(TurnPhase.Draw);
        var engine = SeededEngine();

        var result = engine.Execute(state, new GameAction(GameActionType.DrawCard, 0));

        Assert.False(result.Success);
        Assert.Equal("Game.DeckOut", result.Error);
        Assert.Equal(GameStatus.Finished, state.Status);
        Assert.Equal(1, state.WinnerPlayerIndex);
        Assert.Contains(GameEvent.GameOver, result.Events!);
    }

    [Fact]
    public void PassTurn_WithEmptyDeck_CannotEscapeDeckOut()
    {
        var state = NewState();
        var board = state.Players[0];
        board.Reset();
        board.PlaceActive(new PokemonInPlay(TestCards.BasicPokemon("solo", "Solo", 60)));
        state.SetPhase(TurnPhase.Draw);
        var engine = SeededEngine();

        var result = engine.Execute(state, new GameAction(GameActionType.PassTurn, 0));

        Assert.False(result.Success);
        Assert.Equal("Game.DeckOut", result.Error);
        Assert.Equal(GameStatus.Finished, state.Status);
        Assert.Equal(1, state.WinnerPlayerIndex);
    }

    // ---------------------------------------------------------- BUG-D009
    [Fact]
    public void SuddenDeath_RestartsAtTurnOne()
    {
        var state = NewState();
        while (state.Players[0].TakePrize().IsSuccess) { }
        while (state.Players[1].TakePrize().IsSuccess) { }
        var engine = SeededEngine();

        var result = engine.Execute(state, new GameAction(GameActionType.PassTurn, 0));

        Assert.True(result.Success, result.Error);
        Assert.True(state.SuddenDeath);
        Assert.Equal(GameStatus.Setup, state.Status);
        Assert.Equal(1, state.TurnNumber);
        Assert.Equal(TurnPhase.Draw, state.Phase);
        Assert.Equal(state.StartingPlayerIndex, state.CurrentPlayerIndex);
        Assert.False(state.AllSetupConfirmed);
        Assert.Equal(0, state.MulliganCounts[0]);
        Assert.Equal(-1, state.SupportersBlockedUntilTurn[0]);
        Assert.Equal(7, state.Players[0].Hand.Count);
        Assert.Equal(7, state.Players[1].Hand.Count);
    }

    // ---------------------------------------------------------- BUG-D010
    [Fact]
    public void PassTurn_EmitsTurnEndAndTurnStart()
    {
        var state = NewState();
        var engine = SeededEngine();

        var result = engine.Execute(state, new GameAction(GameActionType.PassTurn, 0));

        Assert.True(result.Success, result.Error);
        Assert.Contains(GameEvent.TurnEnd, result.Events!);
        Assert.Contains(GameEvent.TurnStart, result.Events!);
    }

    [Fact]
    public void AttachEnergy_EmitsEnergyAttached()
    {
        var state = NewState();
        var board = state.Players[0];
        var fire = new EnergyCard("f1", "Fire Energy", [], [], "xy1", "",
            PokemonType.Fire, null);
        board.AddToHand(fire);
        var engine = SeededEngine();

        var result = engine.Execute(state,
            new GameAction(GameActionType.AttachEnergy, 0, Card: fire, Target: board.Active));

        Assert.True(result.Success, result.Error);
        Assert.Contains(GameEvent.EnergyAttached, result.Events!);
    }

    [Fact]
    public void Evolve_EmitsEvolution()
    {
        var state = NewState();
        var board = state.Players[0];
        var basePokemon = new PokemonInPlay(TestCards.BasicPokemon("base", "Base", 60));
        basePokemon.JustPlayed = false;
        board.AddToBench(basePokemon);
        var evolution = new PokemonCard("evo", "Evo", [SubType.Stage1], [], "xy1",
            string.Empty, 90, 1, EvolvesFrom: "Base", PokemonType.Colorless, [], [], [], []);
        board.AddToHand(evolution);
        var engine = SeededEngine();

        var result = engine.Execute(state,
            new GameAction(GameActionType.Evolve, 0, Card: evolution, Target: basePokemon));

        Assert.True(result.Success, result.Error);
        Assert.Contains(GameEvent.Evolution, result.Events!);
    }

    // ---------------------------------------------------------- BUG-D012
    [Fact]
    public void ForestsCurse_BlocksOpponentItems()
    {
        var state = NewState();
        var trevenant = new PokemonCard(
            "trev", "Trevenant", [SubType.Stage1], [], "xy1", string.Empty, 90, 1,
            EvolvesFrom: "Phantump", PokemonType.Grass, [],
            [new PokemonAbility("Forest's Curse", "Items blocked.", AbilityType.Passive)],
            [], []);
        state.Players[1].ClearActive();
        state.Players[1].PlaceActive(new PokemonInPlay(trevenant));

        var rules = new RuleValidator();
        var item = new TrainerCard("potion", "Potion", [SubType.Item], [], "xy1", string.Empty, SubType.Item);
        var supporter = new TrainerCard("prof", "Professor", [SubType.Supporter], [], "xy1", string.Empty, SubType.Supporter);

        Assert.Equal("Trainer.ItemsRestricted",
            rules.CanPlayTrainer(state, 0, item, null)?.Code);
        Assert.Null(rules.CanPlayTrainer(state, 0, supporter, null));
    }

    // ---------------------------------------------------------- BUG-D013
    [Fact]
    public void SuperPotion_HealsAndDiscardsOnChosenTarget()
    {
        var state = NewState();
        var board = state.Players[0];
        var bench = new PokemonInPlay(TestCards.BasicPokemon("b", "B", 60));
        bench.AddDamage(60);
        bench.AttachEnergy(TestCards.BasicEnergy("e1", PokemonType.Water));
        board.AddToBench(bench);
        board.Active!.AddDamage(30);
        var potion = new TrainerCard("sp", "Super Potion", [SubType.Item], [],
            "xy1", string.Empty, SubType.Item);
        board.AddToHand(potion);
        var engine = SeededEngine();

        var result = engine.Execute(state,
            new GameAction(GameActionType.PlayCard, 0, Card: potion, Target: bench));

        Assert.True(result.Success, result.Error);
        Assert.Equal(0, bench.TotalDamage);
        Assert.Empty(bench.AttachedEnergies);
        Assert.Equal(30, board.Active.TotalDamage);
    }

    // ---------------------------------------------------------- BUG-D014
    [Fact]
    public void RemoveTool_ClearsItsModifiers()
    {
        var tool = new TrainerCard("mb", "Muscle Band", [SubType.Tool], [],
            "xy1", string.Empty, SubType.Tool);
        var pokemon = new PokemonInPlay(TestCards.BasicPokemon("x", "X", 100));
        pokemon.AttachTool(tool);
        pokemon.AddModifier(new BoardModifier(
            ModifierType.DamageBoost, "Muscle Band", 20, ModifierDuration.Permanent, 0));
        pokemon.AddModifier(new BoardModifier(
            ModifierType.CannotAttack, "Other", 0, ModifierDuration.UntilNextTurn, 1));

        var removed = pokemon.RemoveTool();

        Assert.Equal("Muscle Band", removed!.Name);
        Assert.DoesNotContain(pokemon.Modifiers, m => m.Source == "Muscle Band");
        Assert.Contains(pokemon.Modifiers, m => m.Source == "Other");
    }

    // ---------------------------------------------------------- BUG-D015
    [Fact]
    public void Retreat_WithInvalidTarget_FailsWithoutDiscardingEnergies()
    {
        var state = NewState();
        var board = state.Players[0];
        board.AddToBench(new PokemonInPlay(TestCards.BasicPokemon("b", "B", 60)));
        var active = board.Active!;
        var energy = TestCards.BasicEnergy("e1", PokemonType.Water);
        active.AttachEnergy(energy);
        var engine = SeededEngine();

        // El "reemplazo" es el propio activo: inválido, y antes de mutar.
        var result = engine.Execute(state, new GameAction(GameActionType.Retreat, 0,
            Target: active, EnergiesToDiscard: [energy]));

        Assert.False(result.Success);
        Assert.Equal("Retreat.InvalidTarget", result.Error);
        Assert.Contains(energy, active.AttachedEnergies);
    }

    // ---------------------------------------------------------- BUG-D016
    [Fact]
    public void Promotion_KeepsJustPlayed_CannotEvolveSameTurn()
    {
        var state = NewState();
        var board = state.Players[0];
        board.ClearActive();
        var low = new PokemonInPlay(TestCards.BasicPokemon("low", "Low", 10));
        board.PlaceActive(low);
        var fresh = new PokemonInPlay(TestCards.BasicPokemon("fresh", "Fresh", 60));
        board.AddToBench(fresh); // JustPlayed = true (jugado este turno)
        var rainbow = new EnergyCard("rb", "Rainbow Energy", [], [], "xy1", "",
            PokemonType.Colorless, SpecialEnergyType.Rainbow);
        board.AddToHand(rainbow);
        var engine = SeededEngine();

        engine.Execute(state,
            new GameAction(GameActionType.AttachEnergy, 0, Card: rainbow, Target: low));

        Assert.Same(fresh, board.Active);
        Assert.True(board.Active.JustPlayed);
        var evolution = new PokemonCard("evo", "Evo", [SubType.Stage1], [], "xy1",
            string.Empty, 90, 1, EvolvesFrom: "Fresh", PokemonType.Colorless, [], [], [], []);
        var err = new RuleValidator().CanEvolve(state, board.Active, evolution);
        Assert.Equal("Evolve.JustPlayed", err?.Code);
    }

    // ---------------------------------------------------------- BUG-D017
    [Fact]
    public void CanRetreat_UnattachedEnergy_Rejected()
    {
        var state = NewState();
        var unattached = TestCards.BasicEnergy("ghost", PokemonType.Water);

        var err = new RuleValidator().CanRetreat(state, 0, [unattached]);

        Assert.Equal("Retreat.EnergyNotAttached", err?.Code);
    }

    [Fact]
    public void OptionalDiscard_WithUnattachedEnergy_Rejected()
    {
        // El +30 de Electron Crush exige descartar energía adjunta real.
        var state = ElectronCrushState();
        var engine = SeededEngine();
        var pending = StartElectronCrush(state, engine, out _);
        var ghost = TestCards.BasicEnergy("ghost", PokemonType.Water);

        var resume = engine.Execute(state, new GameAction(GameActionType.Attack, 0,
            Attack: ElectronCrush(), ChoiceId: pending.ChoiceId,
            SelectedOptionIds: ["yes"], EnergiesToDiscard: [ghost]));

        Assert.False(resume.Success);
        Assert.Equal("Attack.EnergyNotAttached", resume.Error);
        Assert.Equal(2, state.Players[0].Active!.AttachedEnergies.Count);
        Assert.Equal(0, state.Players[1].Active!.TotalDamage);
    }

    // ---------------------------------------------------------- BUG-D018
    [Fact]
    public void PlayPokemon_NotInHand_Rejected()
    {
        var state = NewState();
        var board = state.Players[0];
        var ghost = TestCards.BasicPokemon("ghost", "Ghost", 60);
        var engine = SeededEngine();

        var result = engine.Execute(state, new GameAction(GameActionType.PlayCard, 0, Card: ghost));

        Assert.False(result.Success);
        Assert.Equal("Play.NotInHand", result.Error);
        Assert.Empty(board.Bench);
    }

    [Fact]
    public void AttachEnergy_NotInHand_Rejected()
    {
        var state = NewState();
        var board = state.Players[0];
        var ghost = TestCards.BasicEnergy("ghost", PokemonType.Fire);
        var engine = SeededEngine();

        var result = engine.Execute(state,
            new GameAction(GameActionType.AttachEnergy, 0, Card: ghost, Target: board.Active));

        Assert.False(result.Success);
        Assert.Equal("Energy.NotInHand", result.Error);
        Assert.Empty(board.Active!.AttachedEnergies);
    }

    [Fact]
    public void Evolve_ForeignTarget_Rejected()
    {
        var state = NewState();
        var opponent = state.Players[1].Active!;
        var evolution = new PokemonCard("evo", "Evo", [SubType.Stage1], [], "xy1",
            string.Empty, 90, 1, EvolvesFrom: opponent.Card.Name, PokemonType.Colorless, [], [], [], []);
        state.Players[0].AddToHand(evolution);
        var engine = SeededEngine();

        var result = engine.Execute(state,
            new GameAction(GameActionType.Evolve, 0, Card: evolution, Target: opponent));

        Assert.False(result.Success);
        Assert.Equal("Evolve.ForeignTarget", result.Error);
    }

    [Fact]
    public void AttachTool_ForeignTarget_Rejected()
    {
        var state = NewState();
        var board = state.Players[0];
        var tool = new TrainerCard("mb", "Muscle Band", [SubType.Tool], [],
            "xy1", string.Empty, SubType.Tool);
        board.AddToHand(tool);
        var opponent = state.Players[1].Active!;
        var engine = SeededEngine();

        var result = engine.Execute(state,
            new GameAction(GameActionType.PlayCard, 0, Card: tool, Target: opponent));

        Assert.False(result.Success);
        Assert.Equal("Trainer.ForeignTarget", result.Error);
        Assert.Contains(tool, board.Hand);
    }

    [Fact]
    public void UseAbility_ForeignUser_Rejected()
    {
        var state = NewState();
        var opponent = state.Players[1].Active!;
        var engine = SeededEngine();

        var result = engine.Execute(state, new GameAction(GameActionType.UseAbility, 0,
            Target: opponent, AbilityName: "Any Ability"));

        Assert.False(result.Success);
        Assert.Equal("Ability.ForeignTarget", result.Error);
    }

    // ---------------------------------------------------------- BUG-D019
    [Fact]
    public void PerformMulligan_AfterConfirm_Rejected()
    {
        var state = new GameState(new PlayerBoard(), new PlayerBoard(), 0, suddenDeath: false);
        state.ConfirmSetup(0);
        var engine = SeededEngine();

        var error = engine.PerformMulligan(state, 0);

        Assert.NotNull(error);
        Assert.Equal("Setup.AlreadyConfirmed", error!.Code);
    }

    [Fact]
    public void PerformMulligan_OutsideSetup_Rejected()
    {
        var state = NewState(); // InProgress
        var engine = SeededEngine();

        var error = engine.PerformMulligan(state, 0);

        Assert.NotNull(error);
        Assert.Equal("Game.NotJoinable", error!.Code);
    }

    // ---------------------------------------------------------- BUG-D020
    [Fact]
    public void Concede_IsAllowedDuringSetup_EvenOutOfTurn()
    {
        var state = new GameState(new PlayerBoard(), new PlayerBoard(), 0, suddenDeath: false);
        var engine = SeededEngine();

        // El jugador1 no es el actual (0) y aun así puede rendirse.
        var result = engine.Execute(state, new GameAction(GameActionType.Concede, 1));

        Assert.True(result.Success, result.Error);
        Assert.Equal(GameStatus.Finished, state.Status);
        Assert.Equal(0, state.WinnerPlayerIndex);
    }
}
