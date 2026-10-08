using DotnetTCG.Domain.Attacks;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Tests;

public sealed class AttackPipelineTests
{
    private static Attack MakeAttack(string name, string damage, int cost = 1)
        => new(name, string.Empty, damage,
            Enumerable.Repeat(PokemonType.Colorless, cost).ToList());

    private static (GameState State, AttackContext Ctx) NewContext(
        string attackName = "Tackle",
        string damage = "30",
        int attackerEnergy = 1)
    {
        var state = new GameState(new PlayerBoard(), new PlayerBoard(), 0, suddenDeath: false);
        state.SetStatus(GameStatus.InProgress);
        var attacker = new PokemonInPlay(TestCards.BasicPokemon("a", "A", hp: 100));
        for (var i = 0; i < attackerEnergy; i++)
            attacker.AttachEnergy(TestCards.BasicEnergy($"e{i}", PokemonType.Colorless));
        attacker.JustPlayed = false;
        var defender = new PokemonInPlay(TestCards.BasicPokemon("d", "D", hp: 100));
        state.Players[0].PlaceActive(attacker);
        state.Players[1].PlaceActive(defender);
        var ctx = new AttackContext
        {
            State = state,
            ActingPlayerIndex = 0,
            Attack = MakeAttack(attackName, damage),
            Attacker = attacker,
            Defender = defender,
        };
        return (state, ctx);
    }

    private static AttackPipeline Pipeline(ICoinFlipService? coin = null)
        => new(new DamageCalculator(), new EffectEngine(
            new StatusEffectManager(), coin ?? new SeededCoinFlip(), new Random(5)),
            new StatusEffectManager(), coin ?? new SeededCoinFlip());

    private static readonly AttackRegistry Registry = new();

    [Fact]
    public void ParseBaseDamage_ParsesLeadingDigits()
    {
        Assert.Equal(30, AttackPipeline.ParseBaseDamage("30"));
        Assert.Equal(30, AttackPipeline.ParseBaseDamage("30+"));
        Assert.Equal(20, AttackPipeline.ParseBaseDamage("20×"));
        Assert.Equal(0, AttackPipeline.ParseBaseDamage(string.Empty));
    }

    [Fact]
    public void Execute_UnknownAttack_DealsBaseDamage()
    {
        var (_, ctx) = NewContext();

        var result = Pipeline().Execute(ctx, Registry.Find("Tackle"));

        Assert.True(result.Success);
        Assert.Equal(30, result.DamageDealt);
        Assert.Equal(30, ctx.Defender.TotalDamage);
    }

    [Fact]
    public void Execute_HyperFangTails_FailsWithoutDamage()
    {
        var (_, ctx) = NewContext("Hyper Fang", "60");

        var result = Pipeline(new SeededCoinFlip(false)).Execute(ctx, Registry.Find("Hyper Fang"));

        Assert.False(result.Success);
        Assert.Equal("Attack.FlipFail", result.FailureReason);
        Assert.Equal(0, ctx.Defender.TotalDamage);
    }

    [Fact]
    public void Execute_EnergyShort_Fails()
    {
        var (_, ctx) = NewContext(attackerEnergy: 0);

        var result = Pipeline().Execute(ctx, Registry.Find("Tackle"));

        Assert.False(result.Success);
        Assert.Equal("Attack.EnergyShort", result.FailureReason);
    }

    [Fact]
    public void Execute_ConfusedTails_SelfHitAndFail()
    {
        var (_, ctx) = NewContext();
        ctx.Attacker.AddStatus(Status.Confused);

        var result = Pipeline(new SeededCoinFlip(false)).Execute(ctx, Registry.Find("Tackle"));

        Assert.False(result.Success);
        Assert.Equal("Attack.ConfusedSelfHit", result.FailureReason);
        Assert.Equal(30, ctx.Attacker.TotalDamage);
        Assert.Equal(0, ctx.Defender.TotalDamage);
    }

    [Fact]
    public void Execute_ElectronCrushWithoutDecision_ReturnsPending()
    {
        var (_, ctx) = NewContext("Electron Crush", "50");

        var result = Pipeline().Execute(ctx, Registry.Find("Electron Crush"));

        Assert.NotNull(result.Pending);
        Assert.Contains("optional-discard", result.Pending.ChoiceId);
    }

    [Fact]
    public void Execute_ElectronCrushAccepted_Adds30()
    {
        var (_, ctx) = NewContext("Electron Crush", "50");
        ctx.OptionalDiscardAccepted = true;

        var result = Pipeline().Execute(ctx, Registry.Find("Electron Crush"));

        Assert.True(result.Success);
        Assert.Equal(80, result.DamageDealt);
        Assert.Empty(ctx.Attacker.AttachedEnergies);
    }

    [Fact]
    public void Execute_BiteOffVsEx_Adds60()
    {
        var (state, ctx) = NewContext("Bite Off", "30");
        var exCard = TestCards.BasicPokemon("ex", "EX", hp: 170) with
        {
            Subtypes = [SubType.Basic, SubType.Ex],
        };
        var exDefender = new PokemonInPlay(exCard);
        state.Players[1].ClearActive();
        state.Players[1].PlaceActive(exDefender);
        ctx = new AttackContext
        {
            State = state,
            ActingPlayerIndex = 0,
            Attack = MakeAttack("Bite Off", "30"),
            Attacker = ctx.Attacker,
            Defender = exDefender,
        };

        var result = Pipeline().Execute(ctx, Registry.Find("Bite Off"));

        Assert.True(result.Success);
        Assert.Equal(90, result.DamageDealt);
    }

    [Fact]
    public void Execute_SpikyShield_Retaliates30()
    {
        var (state, ctx) = NewContext();
        var shieldCard = TestCards.BasicPokemon("s", "Shield", hp: 100) with
        {
            Abilities = [new PokemonAbility("Spiky Shield", "text", AbilityType.Passive)],
        };
        var shield = new PokemonInPlay(shieldCard);
        state.Players[1].ClearActive();
        state.Players[1].PlaceActive(shield);
        ctx = new AttackContext
        {
            State = state,
            ActingPlayerIndex = 0,
            Attack = MakeAttack("Tackle", "30"),
            Attacker = ctx.Attacker,
            Defender = shield,
        };

        Pipeline().Execute(ctx, Registry.Find("Tackle"));

        Assert.Equal(30, ctx.Attacker.TotalDamage);
    }

    [Fact]
    public void Execute_NeedsBenchTargetWithoutSelection_ReturnsPending()
    {
        var (_, ctx) = NewContext();
        ctx.Defender.JustPlayed = false;
        var behavior = new AttackBehavior(NeedsBenchTarget: EffectTarget.OpponentBench);
        var bench = new PokemonInPlay(TestCards.BasicPokemon("b", "B", hp: 60));
        ctx.State.Players[1].AddToBench(bench);

        var result = Pipeline().Execute(ctx, behavior);

        Assert.NotNull(result.Pending);
        Assert.Single(result.Pending.Options);
    }

    [Fact]
    public void Resume_WithBenchSelection_Completes()
    {
        var (_, ctx) = NewContext("Snipe", "20");
        var behavior = new AttackBehavior(NeedsBenchTarget: EffectTarget.OpponentBench);
        var bench = new PokemonInPlay(TestCards.BasicPokemon("b", "B", hp: 60));
        ctx.State.Players[1].AddToBench(bench);
        var pipeline = Pipeline();

        var pending = pipeline.Execute(ctx, behavior);

        Assert.NotNull(pending.Pending);
        ctx.SelectedTarget = bench;

        var result = pipeline.Resume(ctx, behavior);

        Assert.True(result.Success);
        Assert.Equal(20, ctx.Defender.TotalDamage);
    }
}
