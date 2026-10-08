using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Tests;

public sealed class RuleValidatorTests
{
    private static readonly RuleValidator Rules = new();

    private static List<Card> ValidDeck()
    {
        var cards = new List<Card> { TestCards.BasicPokemon("xy1-1", "Pikachu", hp: 60) };
        for (var i = 0; i < 59; i++)
            cards.Add(TestCards.BasicEnergy($"e-{i}", PokemonType.Lightning));
        return cards;
    }

    private static GameState NewStateWithActive(
        int startingPlayer = 0,
        PokemonCard? activeCard = null,
        int activeEnergy = 0)
    {
        var state = new GameState(new PlayerBoard(), new PlayerBoard(), startingPlayer, suddenDeath: false);
        var active = new PokemonInPlay(activeCard ?? TestCards.BasicPokemon("xy1-1", "Pikachu", hp: 60))
        {
            JustPlayed = false,
        };
        for (var i = 0; i < activeEnergy; i++)
            active.AttachEnergy(TestCards.BasicEnergy($"e-{i}", PokemonType.Colorless));
        state.Players[0].PlaceActive(active);
        return state;
    }

    [Fact]
    public void ValidateDeck_Valid60_ReturnsNoErrors()
    {
        Assert.Empty(Rules.ValidateDeck(ValidDeck()));
    }

    [Fact]
    public void ValidateDeck_WrongSize_ReturnsSizeError()
    {
        var cards = ValidDeck().Take(59).ToList();

        var errors = Rules.ValidateDeck(cards);

        Assert.Contains(errors, e => e.Code == "Deck.Size");
    }

    [Fact]
    public void ValidateDeck_NoBasic_ReturnsError()
    {
        var cards = new List<Card>();
        for (var i = 0; i < 60; i++)
            cards.Add(TestCards.BasicEnergy($"e-{i}", PokemonType.Fire));

        var errors = Rules.ValidateDeck(cards);

        Assert.Contains(errors, e => e.Code == "Deck.NoBasic");
    }

    [Fact]
    public void ValidateDeck_FiveCopiesNonBasic_ReturnsError()
    {
        var cards = ValidDeck();
        for (var i = 0; i < 5; i++)
            cards.Add(TestCards.Supporter($"s-{i}", "Tierno"));
        cards.RemoveRange(1, 5);

        var errors = Rules.ValidateDeck(cards);

        Assert.Contains(errors, e => e.Code == "Deck.MaxCopies");
    }

    [Fact]
    public void ValidateDeck_ManyBasicEnergies_NoCopyError()
    {
        var errors = Rules.ValidateDeck(ValidDeck());

        Assert.DoesNotContain(errors, e => e.Code == "Deck.MaxCopies");
    }

    [Fact]
    public void CanPlayPokemonToBench_NonBasic_ReturnsError()
    {
        var state = NewStateWithActive();
        var stage1 = TestCards.Stage1Pokemon("xy1-2", "Raichu", "Pikachu", hp: 90);

        Assert.Equal("Play.NotBasic", Rules.CanPlayPokemonToBench(state, 0, stage1)?.Code);
    }

    [Fact]
    public void CanPlayPokemonToBench_NoActive_ReturnsError()
    {
        var state = new GameState(new PlayerBoard(), new PlayerBoard(), 0, suddenDeath: false);

        Assert.Equal("Play.NoActive",
            Rules.CanPlayPokemonToBench(state, 0, TestCards.BasicPokemon("x", "Y", hp: 60))?.Code);
    }

    [Fact]
    public void CanPlayPokemonToBench_FullBench_ReturnsError()
    {
        var state = NewStateWithActive();
        for (var i = 0; i < PlayerBoard.MaxBenchSize; i++)
            state.Players[0].AddToBench(new PokemonInPlay(TestCards.BasicPokemon($"b-{i}", "B", hp: 60)));

        Assert.Equal("Play.BenchFull",
            Rules.CanPlayPokemonToBench(state, 0, TestCards.BasicPokemon("x", "Y", hp: 60))?.Code);
    }

    [Fact]
    public void CanEvolve_Valid_ReturnsNull()
    {
        var state = NewStateWithActive();
        state.AdvanceTurn();
        var evolution = TestCards.Stage1Pokemon("xy1-2", "Raichu", "Pikachu", hp: 90);

        Assert.Null(Rules.CanEvolve(state, state.Players[0].Active!, evolution));
    }

    [Fact]
    public void CanEvolve_FirstTurnStartingPlayer_Blocked()
    {
        var state = NewStateWithActive();
        var evolution = TestCards.Stage1Pokemon("xy1-2", "Raichu", "Pikachu", hp: 90);

        Assert.Equal("Evolve.FirstTurn", Rules.CanEvolve(state, state.Players[0].Active!, evolution)?.Code);
    }

    [Fact]
    public void CanEvolve_JustPlayed_Blocked()
    {
        var state = NewStateWithActive();
        state.AdvanceTurn();
        state.Players[0].Active!.JustPlayed = true;
        var evolution = TestCards.Stage1Pokemon("xy1-2", "Raichu", "Pikachu", hp: 90);

        Assert.Equal("Evolve.JustPlayed", Rules.CanEvolve(state, state.Players[0].Active!, evolution)?.Code);
    }

    [Fact]
    public void CanAttachEnergy_Twice_Blocked()
    {
        var state = NewStateWithActive();

        Assert.Null(Rules.CanAttachEnergy(state));
        state.RegisterEnergyAttached();

        Assert.Equal("Energy.OncePerTurn", Rules.CanAttachEnergy(state)?.Code);
    }

    [Fact]
    public void CanPlayTrainer_SecondSupporter_Blocked()
    {
        var state = NewStateWithActive();
        var supporter = TestCards.Supporter("s-1", "Tierno");

        Assert.Null(Rules.CanPlayTrainer(state, 0, supporter));
        state.RegisterSupporterPlayed();

        Assert.Equal("Trainer.SupporterOnce", Rules.CanPlayTrainer(state, 0, supporter)?.Code);
    }

    [Fact]
    public void CanPlayTrainer_SecondStadium_Blocked()
    {
        var state = NewStateWithActive();
        var stadium = TestCards.Stadium("st-1", "Fairy Garden");

        Assert.Null(Rules.CanPlayTrainer(state, 0, stadium));
        state.RegisterStadiumPlayed();

        Assert.Equal("Trainer.StadiumOnce", Rules.CanPlayTrainer(state, 0, stadium)?.Code);
    }

    [Fact]
    public void CanPlayTrainer_ToolWithoutTarget_Blocked()
    {
        var state = NewStateWithActive();

        Assert.Equal("Trainer.ToolNoTarget",
            Rules.CanPlayTrainer(state, 0, TestCards.Tool("t-1", "Muscle Band"))?.Code);
    }

    [Fact]
    public void CanAttack_EnergyShort_Blocked()
    {
        var state = NewStateWithActive(activeEnergy: 0);
        state.AdvanceTurn();
        var attack = new Attack("Tackle", string.Empty, "30", [PokemonType.Colorless]);

        Assert.Equal("Attack.EnergyShort", Rules.CanAttack(state, 0, attack)?.Code);
    }

    [Fact]
    public void CanAttack_WithEnergy_ReturnsNull()
    {
        var state = NewStateWithActive(activeEnergy: 1);
        state.AdvanceTurn();
        var attack = new Attack("Tackle", string.Empty, "30", [PokemonType.Colorless]);

        Assert.Null(Rules.CanAttack(state, 0, attack));
    }

    [Fact]
    public void CanRetreat_NoBench_Blocked()
    {
        var state = NewStateWithActive(activeEnergy: 1);

        Assert.Equal("Retreat.NoBench", Rules.CanRetreat(state, 0, [])?.Code);
    }

    [Fact]
    public void CanRetreat_WithCostPaid_ReturnsNull()
    {
        var state = NewStateWithActive(activeEnergy: 2);
        state.Players[0].AddToBench(new PokemonInPlay(TestCards.BasicPokemon("b", "B", hp: 60)));
        var energies = state.Players[0].Active!.AttachedEnergies.ToList();

        Assert.Null(Rules.CanRetreat(state, 0, energies));
    }

    [Fact]
    public void CanUseAbility_SecondUse_Blocked()
    {
        var state = NewStateWithActive();
        var card = TestCards.BasicPokemon("xy1-1", "Pikachu", hp: 60);
        var withAbility = card with
        {
            Abilities = [new PokemonAbility("Static", "text", AbilityType.Activated)],
        };
        var pokemon = new PokemonInPlay(withAbility);

        Assert.Null(Rules.CanUseAbility(state, pokemon, "Static"));
        state.RegisterAbilityUsed();

        Assert.Equal("Ability.OncePerTurn", Rules.CanUseAbility(state, pokemon, "Static")?.Code);
    }

    [Fact]
    public void CanPlayPokemonToBench_Valid_ReturnsNull()
    {
        var state = NewStateWithActive();

        Assert.Null(Rules.CanPlayPokemonToBench(
            state, 0, TestCards.BasicPokemon("x", "Y", hp: 60)));
    }

    [Fact]
    public void CanEvolve_Stage2Target_Blocked()
    {
        var state = NewStateWithActive();
        state.AdvanceTurn();
        var stage2 = TestCards.Stage1Pokemon("s2", "Stage2", "Raichu", hp: 120) with
        {
            Subtypes = [SubType.Stage2],
        };
        var target = new PokemonInPlay(stage2) { JustPlayed = false };
        var evolution = TestCards.Stage1Pokemon("mega", "Mega", "Stage2", hp: 200);

        Assert.Equal("Evolve.MaxStage", Rules.CanEvolve(state, target, evolution)?.Code);
    }

    [Fact]
    public void CanEvolve_NameMismatch_Blocked()
    {
        var state = NewStateWithActive();
        state.AdvanceTurn();
        var evolution = TestCards.Stage1Pokemon("xy1-2", "Raichu", "Other", hp: 90);

        Assert.Equal("Evolve.Mismatch", Rules.CanEvolve(state, state.Players[0].Active!, evolution)?.Code);
    }

    [Fact]
    public void CanPlayTrainer_SupportersBlocked_Blocked()
    {
        var state = NewStateWithActive();
        state.BlockSupporters(0, state.TurnNumber);

        Assert.Equal("Trainer.SupporterBlocked",
            Rules.CanPlayTrainer(state, 0, TestCards.Supporter("s", "Tierno"))?.Code);
    }

    [Fact]
    public void CanPlayTrainer_ToolOccupied_Blocked()
    {
        var state = NewStateWithActive();
        var target = state.Players[0].Active!;
        target.AttachTool(TestCards.Tool("t0", "Tool"));

        Assert.Equal("Trainer.ToolOccupied",
            Rules.CanPlayTrainer(state, 0, TestCards.Tool("t-1", "Muscle Band"), target)?.Code);
    }

    [Fact]
    public void CanPlayTrainer_ItemsRestricted_Blocked()
    {
        var state = NewStateWithActive();
        state.Players[0].AddModifier(new BoardModifier(
            ModifierType.RestrictItems, "Effect", 0, ModifierDuration.UntilNextTurn, state.TurnNumber));

        Assert.Equal("Trainer.ItemsRestricted",
            Rules.CanPlayTrainer(state, 0, TestCards.Item("i", "Potion"))?.Code);
    }

    [Fact]
    public void CanRetreat_NoActive_Blocked()
    {
        var state = new GameState(new PlayerBoard(), new PlayerBoard(), 0, suddenDeath: false);

        Assert.Equal("Retreat.NoActive", Rules.CanRetreat(state, 0, [])?.Code);
    }

    [Fact]
    public void CanRetreat_Twice_Blocked()
    {
        var state = NewStateWithActive(activeEnergy: 2);
        state.Players[0].AddToBench(new PokemonInPlay(TestCards.BasicPokemon("b", "B", hp: 60)));
        state.RegisterRetreat();

        Assert.Equal("Retreat.OncePerTurn",
            Rules.CanRetreat(state, 0, state.Players[0].Active!.AttachedEnergies.ToList())?.Code);
    }

    [Fact]
    public void CanRetreat_Asleep_Blocked()
    {
        var state = NewStateWithActive(activeEnergy: 1);
        state.Players[0].AddToBench(new PokemonInPlay(TestCards.BasicPokemon("b", "B", hp: 60)));
        state.Players[0].Active!.AddStatus(Status.Asleep);

        Assert.Equal("Retreat.StatusBlocked", Rules.CanRetreat(state, 0, [])?.Code);
    }

    [Fact]
    public void CanRetreat_CostUnpaid_Blocked()
    {
        var heavy = TestCards.BasicPokemon("h", "Heavy", hp: 100, retreatCost: 2);
        var state = NewStateWithActive(activeCard: heavy, activeEnergy: 1);
        state.Players[0].AddToBench(new PokemonInPlay(TestCards.BasicPokemon("b", "B", hp: 60)));

        Assert.Equal("Retreat.CostUnpaid", Rules.CanRetreat(state, 0, [])?.Code);
    }

    [Fact]
    public void CanRetreat_DiscardMissing_Blocked()
    {
        var heavy = TestCards.BasicPokemon("h", "Heavy", hp: 100, retreatCost: 2);
        var state = NewStateWithActive(activeCard: heavy, activeEnergy: 2);
        state.Players[0].AddToBench(new PokemonInPlay(TestCards.BasicPokemon("b", "B", hp: 60)));

        Assert.Equal("Retreat.DiscardMissing", Rules.CanRetreat(state, 0, [])?.Code);
    }

    [Fact]
    public void CanRetreat_FairyGardenFree_ReturnsNull()
    {
        var fairy = TestCards.BasicPokemon("f", "Fairy", hp: 60, type: PokemonType.Fairy);
        var state = NewStateWithActive(activeCard: fairy, activeEnergy: 0);
        state.Players[0].AddToBench(new PokemonInPlay(TestCards.BasicPokemon("b", "B", hp: 60)));
        state.SetStadium(TestCards.Stadium("st", "Fairy Garden"));

        Assert.Null(Rules.CanRetreat(state, 0, []));
    }

    [Fact]
    public void CanAttack_NoActive_Blocked()
    {
        var state = new GameState(new PlayerBoard(), new PlayerBoard(), 0, suddenDeath: false);
        var attack = new Attack("Tackle", string.Empty, "30", [PokemonType.Colorless]);

        Assert.Equal("Attack.NoActive", Rules.CanAttack(state, 0, attack)?.Code);
    }

    [Fact]
    public void CanAttack_FirstTurnStarting_Blocked()
    {
        var state = NewStateWithActive(activeEnergy: 1);
        var attack = new Attack("Tackle", string.Empty, "30", [PokemonType.Colorless]);

        Assert.Equal("Attack.FirstTurn", Rules.CanAttack(state, 0, attack)?.Code);
    }

    [Fact]
    public void CanAttack_Asleep_Blocked()
    {
        var state = NewStateWithActive(activeEnergy: 1);
        state.AdvanceTurn();
        state.Players[0].Active!.AddStatus(Status.Asleep);
        var attack = new Attack("Tackle", string.Empty, "30", [PokemonType.Colorless]);

        Assert.Equal("Attack.StatusBlocked", Rules.CanAttack(state, 0, attack)?.Code);
    }

    [Fact]
    public void CanAttack_EffectBlocked_Blocked()
    {
        var state = NewStateWithActive(activeEnergy: 1);
        state.AdvanceTurn();
        state.Players[0].Active!.CannotAttackUntilTurn = state.TurnNumber;
        var attack = new Attack("Tackle", string.Empty, "30", [PokemonType.Colorless]);

        Assert.Equal("Attack.Blocked", Rules.CanAttack(state, 0, attack)?.Code);
    }

    [Fact]
    public void CanUseAbility_Asleep_Blocked()
    {
        var state = NewStateWithActive();
        var card = TestCards.BasicPokemon("xy1-1", "Pikachu", hp: 60) with
        {
            Abilities = [new PokemonAbility("Static", "text", AbilityType.Activated)],
        };
        var pokemon = new PokemonInPlay(card);
        pokemon.AddStatus(Status.Asleep);

        Assert.Equal("Ability.StatusBlocked", Rules.CanUseAbility(state, pokemon, "Static")?.Code);
    }

    [Fact]
    public void CanUseAbility_Missing_Blocked()
    {
        var state = NewStateWithActive();
        var pokemon = new PokemonInPlay(TestCards.BasicPokemon("xy1-1", "Pikachu", hp: 60));

        Assert.Equal("Ability.Missing", Rules.CanUseAbility(state, pokemon, "Static")?.Code);
    }
}
