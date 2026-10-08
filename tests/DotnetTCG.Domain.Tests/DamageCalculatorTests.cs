using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Tests;

public sealed class DamageCalculatorTests
{
    private static readonly DamageCalculator Calc = new();

    private static PokemonInPlay Attacker(PokemonType type = PokemonType.Fire)
        => new(TestCards.BasicPokemon("xy1-1", "Attacker", hp: 100, type: type));

    private static PokemonInPlay Defender(
        Weakness[]? weaknesses = null,
        Resistance[]? resistances = null,
        PokemonAbility[]? abilities = null)
    {
        var card = TestCards.BasicPokemon("xy1-2", "Defender", hp: 100) with
        {
            Weaknesses = weaknesses ?? [],
            Resistances = resistances ?? [],
            Abilities = abilities ?? [],
        };
        return new PokemonInPlay(card);
    }

    private static DamageCalculationInput Input(
        int baseDamage,
        PokemonInPlay attacker,
        PokemonInPlay defender,
        string attackName = "Tackle",
        TrainerCard? stadium = null)
        => new(baseDamage, attackName, attacker, defender, stadium);

    [Fact]
    public void Calculate_NoModifiers_ReturnsBase()
    {
        Assert.Equal(30, Calc.Calculate(Input(30, Attacker(), Defender())));
    }

    [Fact]
    public void Calculate_AttackerBoost_AddsValue()
    {
        var attacker = Attacker();
        attacker.AddModifier(new BoardModifier(ModifierType.DamageBoost, "Muscle Band", 20,
            ModifierDuration.Permanent, TurnApplied: 1));

        Assert.Equal(50, Calc.Calculate(Input(30, attacker, Defender())));
    }

    [Fact]
    public void Calculate_MatchingWeakness_Doubles()
    {
        var defender = Defender(weaknesses: [new Weakness(PokemonType.Fire, "×2")]);

        Assert.Equal(60, Calc.Calculate(Input(30, Attacker(PokemonType.Fire), defender)));
    }

    [Fact]
    public void Calculate_WeaknessMismatch_NoChange()
    {
        var defender = Defender(weaknesses: [new Weakness(PokemonType.Water, "×2")]);

        Assert.Equal(30, Calc.Calculate(Input(30, Attacker(PokemonType.Fire), defender)));
    }

    [Fact]
    public void Calculate_MistSlash_IgnoresWeakness()
    {
        var defender = Defender(weaknesses: [new Weakness(PokemonType.Fire, "×2")]);

        Assert.Equal(30, Calc.Calculate(Input(30, Attacker(PokemonType.Fire), defender, "Mist Slash")));
    }

    [Fact]
    public void Calculate_ShadowCircleNullifiesDarknessWeakness()
    {
        var defender = Defender(weaknesses: [new Weakness(PokemonType.Darkness, "×2")]);
        var stadium = TestCards.Stadium("st-1", "Shadow Circle");

        Assert.Equal(30, Calc.Calculate(Input(30, Attacker(PokemonType.Darkness), defender, stadium: stadium)));
    }

    [Fact]
    public void Calculate_MatchingResistance_Subtracts20()
    {
        var defender = Defender(resistances: [new Resistance(PokemonType.Fire, "-20")]);

        Assert.Equal(10, Calc.Calculate(Input(30, Attacker(PokemonType.Fire), defender)));
    }

    [Fact]
    public void Calculate_DefenderReduction_SubtractsValue()
    {
        var defender = Defender();
        defender.AddModifier(new BoardModifier(ModifierType.DamageReduction, "Hard Charm", 20,
            ModifierDuration.Permanent, TurnApplied: 1));

        Assert.Equal(10, Calc.Calculate(Input(30, Attacker(), defender)));
    }

    [Fact]
    public void Calculate_FurCoat_Subtracts20()
    {
        var defender = Defender(abilities: [new PokemonAbility("Fur Coat", "text", AbilityType.Passive)]);

        Assert.Equal(10, Calc.Calculate(Input(30, Attacker(), defender)));
    }

    [Fact]
    public void Calculate_FullPipeline_BoostWeakResReductionFloored()
    {
        var attacker = Attacker(PokemonType.Fire);
        attacker.AddModifier(new BoardModifier(ModifierType.DamageBoost, "Muscle Band", 20,
            ModifierDuration.Permanent, TurnApplied: 1));
        var defender = Defender(
            weaknesses: [new Weakness(PokemonType.Fire, "×2")],
            resistances: [new Resistance(PokemonType.Fire, "-20")]);
        defender.AddModifier(new BoardModifier(ModifierType.DamageReduction, "Hard Charm", 20,
            ModifierDuration.Permanent, TurnApplied: 1));

        Assert.Equal(60, Calc.Calculate(Input(30, attacker, defender)));
    }

    [Fact]
    public void Calculate_NegativeDamage_FloorsAtZero()
    {
        var defender = Defender(resistances: [new Resistance(PokemonType.Fire, "-20")]);

        Assert.Equal(0, Calc.Calculate(Input(10, Attacker(PokemonType.Fire), defender)));
    }
}
