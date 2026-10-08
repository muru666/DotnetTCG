using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Tests;

internal sealed class SeededCoinFlip(params bool[] flips) : ICoinFlipService
{
    private readonly Queue<bool> _flips = new(flips);

    public bool FlipHeads() => _flips.Count > 0 && _flips.Dequeue();
}

public sealed class StatusEffectManagerTests
{
    private static readonly StatusEffectManager Statuses = new();

    private static PokemonInPlay NewPokemon()
        => new(TestCards.BasicPokemon("xy1-1", "Pikachu", hp: 100));

    [Fact]
    public void TryApplyStatus_PoisonAndBurn_Coexist()
    {
        var pokemon = NewPokemon();

        Assert.True(Statuses.TryApplyStatus(pokemon, Status.Poisoned));
        Assert.True(Statuses.TryApplyStatus(pokemon, Status.Burned));

        Assert.True(pokemon.HasStatus(Status.Poisoned));
        Assert.True(pokemon.HasStatus(Status.Burned));
    }

    [Fact]
    public void TryApplyStatus_ExclusiveNewestReplaces()
    {
        var pokemon = NewPokemon();
        Statuses.TryApplyStatus(pokemon, Status.Asleep);

        Statuses.TryApplyStatus(pokemon, Status.Paralyzed);

        Assert.False(pokemon.HasStatus(Status.Asleep));
        Assert.True(pokemon.HasStatus(Status.Paralyzed));
    }

    [Fact]
    public void TryApplyStatus_FairyEnergyImmune()
    {
        var pokemon = NewPokemon();
        pokemon.AttachEnergy(TestCards.BasicEnergy("e-1", PokemonType.Fairy));

        Assert.False(Statuses.TryApplyStatus(pokemon, Status.Poisoned));
        Assert.False(pokemon.HasStatus(Status.Poisoned));
    }

    [Fact]
    public void ProcessBetweenTurns_PoisonDeals10()
    {
        var pokemon = NewPokemon();
        Statuses.TryApplyStatus(pokemon, Status.Poisoned);

        Statuses.ProcessBetweenTurns(pokemon, new SeededCoinFlip());

        Assert.Equal(10, pokemon.TotalDamage);
    }

    [Fact]
    public void ProcessBetweenTurns_BurnTailsDeals20()
    {
        var pokemon = NewPokemon();
        Statuses.TryApplyStatus(pokemon, Status.Burned);

        Statuses.ProcessBetweenTurns(pokemon, new SeededCoinFlip(false));

        Assert.Equal(20, pokemon.TotalDamage);
    }

    [Fact]
    public void ProcessBetweenTurns_BurnHeadsNoDamage()
    {
        var pokemon = NewPokemon();
        Statuses.TryApplyStatus(pokemon, Status.Burned);

        Statuses.ProcessBetweenTurns(pokemon, new SeededCoinFlip(true));

        Assert.Equal(0, pokemon.TotalDamage);
        Assert.True(pokemon.HasStatus(Status.Burned));
    }

    [Fact]
    public void ProcessBetweenTurns_AsleepHeadsWakesUp()
    {
        var pokemon = NewPokemon();
        Statuses.TryApplyStatus(pokemon, Status.Asleep);

        Statuses.ProcessBetweenTurns(pokemon, new SeededCoinFlip(true));

        Assert.False(pokemon.HasStatus(Status.Asleep));
    }

    [Fact]
    public void ProcessBetweenTurns_AsleepTailsStaysAsleep()
    {
        var pokemon = NewPokemon();
        Statuses.TryApplyStatus(pokemon, Status.Asleep);

        Statuses.ProcessBetweenTurns(pokemon, new SeededCoinFlip(false));

        Assert.True(pokemon.HasStatus(Status.Asleep));
    }

    [Fact]
    public void ProcessBetweenTurns_ParalyzedAlwaysCures()
    {
        var pokemon = NewPokemon();
        Statuses.TryApplyStatus(pokemon, Status.Paralyzed);

        Statuses.ProcessBetweenTurns(pokemon, new SeededCoinFlip());

        Assert.False(pokemon.HasStatus(Status.Paralyzed));
    }

    [Fact]
    public void CureOnBenchedOrEvolved_ClearsAll()
    {
        var pokemon = NewPokemon();
        Statuses.TryApplyStatus(pokemon, Status.Burned);
        Statuses.TryApplyStatus(pokemon, Status.Poisoned);
        Statuses.TryApplyStatus(pokemon, Status.Confused);

        Statuses.CureOnBenchedOrEvolved(pokemon);

        Assert.Empty(pokemon.Statuses);
    }
}
