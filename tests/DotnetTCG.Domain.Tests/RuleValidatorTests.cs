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
}
