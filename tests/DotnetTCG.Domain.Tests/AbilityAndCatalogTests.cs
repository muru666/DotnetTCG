using DotnetTCG.Domain.Abilities;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Catalogs;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Tests;

public sealed class AbilityAndCatalogTests
{
    private static readonly AbilityRegistry Abilities = new();
    private static readonly StatusEffectManager Statuses = new();

    private static (GameState State, EffectEngine Effects) NewBattle()
    {
        var state = new GameState(new PlayerBoard(), new PlayerBoard(), 0, suddenDeath: false);
        state.SetStatus(GameStatus.InProgress);
        foreach (var board in state.Players)
        {
            board.LoadDeckForTests(TestHelpers.DeckWithBasics(60));
            board.PlaceActive(new PokemonInPlay(TestCards.BasicPokemon("a", "A", hp: 100)));
        }

        var effects = new EffectEngine(Statuses, new SeededCoinFlip(), new Random(9));
        return (state, effects);
    }

    private static void ApplyAbility(
        EffectEngine effects, GameState state, string name, PokemonInPlay user, PokemonInPlay? target = null)
    {
        var behavior = Abilities.Find(name);
        Assert.NotNull(behavior);
        var ctx = new AbilityContext(state, 0, user, target);
        if (behavior.Effects is not null)
        {
            var effectCtx = new EffectContext(state, 0, user, target);
            foreach (var spec in behavior.Effects)
                effects.Apply(effectCtx, spec);
        }

        behavior.Custom?.Invoke(ctx, effects);
    }

    [Fact]
    public void Find_UnknownAbility_ReturnsNull()
    {
        Assert.Null(Abilities.Find("Nope"));
    }

    [Fact]
    public void MysticalFire_DrawsUntilSix()
    {
        var (state, effects) = NewBattle();
        state.Players[0].AddToHand(TestCards.BasicEnergy("h", PokemonType.Fire));

        ApplyAbility(effects, state, "Mystical Fire", state.Players[0].Active!);

        Assert.Equal(6, state.Players[0].Hand.Count);
    }

    [Fact]
    public void FairyTransfer_MovesFairyToBench()
    {
        var (state, effects) = NewBattle();
        var bench = new PokemonInPlay(TestCards.BasicPokemon("b", "B", hp: 60));
        state.Players[0].AddToBench(bench);
        state.Players[0].Active!.AttachEnergy(TestCards.BasicEnergy("f", PokemonType.Fairy));

        ApplyAbility(effects, state, "Fairy Transfer", state.Players[0].Active!);

        Assert.Empty(state.Players[0].Active.AttachedEnergies);
        Assert.Single(bench.AttachedEnergies);
    }

    [Fact]
    public void DriveOff_ForcesOpponentSwitch()
    {
        var (state, effects) = NewBattle();
        var bench = new PokemonInPlay(TestCards.BasicPokemon("b", "B", hp: 60));
        state.Players[1].AddToBench(bench);

        ApplyAbility(effects, state, "Drive Off", state.Players[0].Active!);

        Assert.Same(bench, state.Players[1].Active);
    }

    [Fact]
    public void UpsideDown_EvolvesConfused()
    {
        var (state, effects) = NewBattle();
        var user = state.Players[0].Active!;
        user.AddStatus(Status.Confused);
        var evolution = TestCards.Stage1Pokemon("xy1-2", "Raichu", "A", hp: 90);
        state.Players[0].AddToHand(evolution);

        ApplyAbility(effects, state, "Upside-Down", user);

        Assert.Equal("Raichu", user.Card.Name);
        Assert.Empty(user.Statuses);
    }

    [Fact]
    public void TrainerCatalog_FindsByNameAndAlias()
    {
        Assert.NotNull(TrainerCatalog.Find("Potion"));
        Assert.NotNull(TrainerCatalog.Find("Tierno"));
        Assert.NotNull(TrainerCatalog.Find("Sycamore"));
        Assert.NotNull(TrainerCatalog.Find("Letter"));
        Assert.NotNull(TrainerCatalog.Find("Muscle Band"));
        Assert.Null(TrainerCatalog.Find("Unknown Trainer"));
    }

    [Fact]
    public void TrainerCatalog_MuscleBand_IsDamageBoostTool()
    {
        var behavior = TrainerCatalog.Find("Muscle Band");

        Assert.NotNull(behavior?.ToolModifier);
        Assert.Equal(ModifierType.DamageBoost, behavior.ToolModifier.Type);
        Assert.Equal(20, behavior.ToolModifier.Value);
    }

    [Fact]
    public void TrainerCatalog_Evosoda_NeedsChoice()
    {
        var behavior = TrainerCatalog.Find("Evosoda");

        Assert.True(behavior?.NeedsChoice);
        Assert.Equal("evosoda", behavior?.ChoiceKind);
    }
}
