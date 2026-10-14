using DotnetTCG.Domain.Attacks;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Tests;

public sealed class AttackRegistryTests
{
    private static readonly AttackRegistry Registry = new();

    private static (GameState State, AttackContext Ctx) NewContext(
        string name, string damage = "50", PokemonType type = PokemonType.Fire, int energy = 1)
    {
        var state = new GameState(new PlayerBoard(), new PlayerBoard(), 0, suddenDeath: false);
        state.SetStatus(GameStatus.InProgress);
        var attacker = new PokemonInPlay(new PokemonCard(
            "a", "Attacker", [SubType.Basic], [], "xy1", "", 100, 1, null, type,
            [new Attack(name, "", damage, [PokemonType.Colorless])], [], [], []));
        for (var i = 0; i < energy; i++)
            attacker.AttachEnergy(TestCards.BasicEnergy($"e{i}", type));
        attacker.JustPlayed = false;
        var defender = new PokemonInPlay(TestCards.BasicPokemon("d", "Defender", hp: 200));
        state.Players[0].PlaceActive(attacker);
        state.Players[1].PlaceActive(defender);
        state.Players[0].AddToBench(new PokemonInPlay(TestCards.BasicPokemon("b0", "Bench0", hp: 60)));
        state.Players[1].AddToBench(new PokemonInPlay(TestCards.BasicPokemon("b1", "Bench1", hp: 60)));
        var ctx = new AttackContext
        {
            State = state,
            ActingPlayerIndex = 0,
            Attack = new Attack(name, "", damage, [PokemonType.Colorless]),
            Attacker = attacker,
            Defender = defender,
        };
        return (state, ctx);
    }

    private static AttackPipeline NewPipeline(ICoinFlipService? flip = null)
        => new(new DamageCalculator(),
            new EffectEngine(new StatusEffectManager(), flip ?? new SeededCoinFlip(), new Random(1)),
            new StatusEffectManager(), flip ?? new SeededCoinFlip(), new Random(1));

    public static TheoryData<string> AllAttacks()
    {
        var data = new TheoryData<string>();
        foreach (var name in new[]
        {
            "Blaze Ball", "Hydro Pump", "Colorful Wind", "Evil Ball", "Balloon Barrage",
            "Circle Circuit", "Cosmic Spin", "Core Splash", "Tailspin", "Bite Off",
            "Wake-Up", "Rage", "Second Bite", "Leaf Munch", "Magma Mantle",
            "Continuous Tumble", "Spiny Rush", "Rock Blast", "Charge Dash", "Electron Crush",
            "Seething Anger", "Hyper Fang", "Rapid Spin", "Splash Bomb", "Conversion Powder",
            "Confusion Wave", "Night Claw", "Flare Blitz", "Fire Tail", "Flame Charge",
            "Energy Glide", "Oblivion", "Geomancy", "Joust", "Y Cyclone",
            "Tricky Steps", "Energy Salon", "Spirit Scream", "Clamp", "Bounce",
            "Hypno", "Recover", "Dig Out", "Seafaring", "Lead Skiddo",
            "Clairvoyant", "Mine", "Pickup", "Bother", "Moonblast",
            "Metal", "Astonish", "Mad Mountain", "Torment", "Darkness Fang",
            "King's Smash", "X Scissor", "Rock Throw",
        })
            data.Add(name);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllAttacks))]
    public void Execute_EveryRegisteredAttack_DoesNotThrow(string name)
    {
        var behavior = Registry.Find(name);
        Assert.NotNull(behavior);
        var (_, ctx) = NewContext(name);
        var heads = new SeededCoinFlip(Enumerable.Repeat(true, 50).ToArray());

        var result = NewPipeline(heads).Execute(ctx, behavior!);

        // Electron Crush exige decisión → pending, no error; el resto debe resolver.
        if (name == "Electron Crush")
            Assert.NotNull(result.Pending);
        else
            Assert.Null(result.FailureReason);
    }

    [Fact]
    public void Find_Unknown_ReturnsNull()
    {
        Assert.Null(Registry.Find("No such attack"));
        Assert.Null(Registry.Find("Tackle"));
    }

    private static int RunFormula(string name, PokemonType attackerType, int energy, string damage = "0")
    {
        var (_, ctx) = NewContext(name, damage, attackerType, energy);
        var result = NewPipeline().Execute(ctx, Registry.Find(name)!);
        Assert.True(result.Success, result.FailureReason);
        return result.DamageDealt;
    }

    [Fact]
    public void BlazeBall_Plus20PerFireEnergy()
        => Assert.Equal(90, RunFormula("Blaze Ball", PokemonType.Fire, 2, "50"));

    [Fact]
    public void HydroPump_Plus20PerWaterEnergy()
        => Assert.Equal(70, RunFormula("Hydro Pump", PokemonType.Water, 1, "50"));

    [Fact]
    public void EvilBall_Plus20PerEnergyOnBothFields()
    {
        // Atacante: 1 energía; defensor con DCE (2) + básica (1) en banca... aquí solo activo: 1.
        // Total campo = 1 (att) + 1 (def) = 2 → 50 + 40 = 90.
        var (state, ctx) = NewContext("Evil Ball", "50", PokemonType.Fire, energy: 1);
        state.Players[1].Active!.AttachEnergy(TestCards.BasicEnergy("de", PokemonType.Water));
        var result = NewPipeline().Execute(ctx, Registry.Find("Evil Ball")!);

        Assert.Equal(90, result.DamageDealt);
    }

    [Fact]
    public void BalloonBarrage_Plus20PerOwnEnergy()
        => Assert.Equal(70, RunFormula("Balloon Barrage", PokemonType.Fire, 1, "50"));

    [Fact]
    public void CircleCircuit_Plus20PerBenchedAlly()
    {
        var (state, ctx) = NewContext("Circle Circuit", "50", PokemonType.Fire, 1);
        state.Players[0].AddToBench(new PokemonInPlay(TestCards.BasicPokemon("b2", "B2", hp: 60)));
        var result = NewPipeline().Execute(ctx, Registry.Find("Circle Circuit")!);
        // 1 bench inicial + 1 añadido = 2 → 50 + 40.
        Assert.Equal(90, result.DamageDealt);
    }

    [Fact]
    public void Rage_Plus10PerOwnDamageCounter()
    {
        var (_, ctx) = NewContext("Rage", "40", PokemonType.Fire, 1);
        ctx.Attacker.AddDamage(50);
        var result = NewPipeline().Execute(ctx, Registry.Find("Rage")!);

        Assert.Equal(90, result.DamageDealt);
    }

    [Fact]
    public void Tailspin_Plus40WhenDefenderDamaged()
    {
        var (_, ctx) = NewContext("Tailspin", "40", PokemonType.Fire, 1);
        ctx.Defender.AddDamage(10);
        var result = NewPipeline().Execute(ctx, Registry.Find("Tailspin")!);

        Assert.Equal(80, result.DamageDealt);
    }

    [Fact]
    public void MagmaMantle_Plus50WhenTopOfDeckIsFireEnergy()
    {
        var (state, ctx) = NewContext("Magma Mantle", "30", PokemonType.Fire, 1);
        var deck = state.Players[0].DeckCards.ToList();
        state.Players[0].LoadDeckForTests(
            deck.Skip(1).Append(TestCards.BasicEnergy("top-fire", PokemonType.Fire)).ToList());
        var result = NewPipeline().Execute(ctx, Registry.Find("Magma Mantle")!);

        Assert.Equal(80, result.DamageDealt);
    }

    [Fact]
    public void SeethingAnger_FlipsPerDamageCounter_HeadsAll()
    {
        var (_, ctx) = NewContext("Seething Anger", "0", PokemonType.Fire, 1);
        ctx.Attacker.AddDamage(20);
        var result = NewPipeline(new SeededCoinFlip(
            Enumerable.Repeat(true, 10).ToArray())).Execute(ctx, Registry.Find("Seething Anger")!);

        Assert.Equal(60, result.DamageDealt); // 2 flips × 30
    }

    [Fact]
    public void ContinuousTumble_FlipsFour_HeadsAll()
    {
        var result = NewPipeline(new SeededCoinFlip(
            Enumerable.Repeat(true, 10).ToArray())).Execute(
            NewContext("Continuous Tumble").Ctx, Registry.Find("Continuous Tumble")!);

        Assert.Equal(120, result.DamageDealt); // 4 × 30
    }

    [Fact]
    public void HyperFang_Tails_Fails()
    {
        var result = NewPipeline(new SeededCoinFlip([false])).Execute(
            NewContext("Hyper Fang").Ctx, Registry.Find("Hyper Fang")!);

        Assert.False(result.Success);
        Assert.Equal("Attack.FlipFail", result.FailureReason);
    }

    [Fact]
    public void ElectronCrush_NoDecision_PendingYesNo()
    {
        var (_, ctx) = NewContext("Electron Crush", "50");

        var result = NewPipeline().Execute(ctx, Registry.Find("Electron Crush")!);

        Assert.NotNull(result.Pending);
        Assert.Equal(2, result.Pending!.Options.Count);
    }

    [Fact]
    public void ElectronCrush_WithoutDiscard_DealsBaseOnly()
    {
        var (_, ctx) = NewContext("Electron Crush", "50");
        ctx.OptionalDiscardAccepted = false;

        var result = NewPipeline().Execute(ctx, Registry.Find("Electron Crush")!);

        Assert.True(result.Success);
        Assert.Equal(50, result.DamageDealt);
        Assert.Single(ctx.Attacker.AttachedEnergies); // nada descartado
    }

    [Fact]
    public void SpiritScream_ReducesBothTo10Hp()
    {
        var (_, ctx) = NewContext("Spirit Scream", "50");
        ctx.Defender.AddDamage(50);

        var result = NewPipeline().Execute(ctx, Registry.Find("Spirit Scream")!);

        Assert.True(result.Success);
        Assert.Equal(10, ctx.Defender.HpRemaining);
        Assert.Equal(10, ctx.Attacker.HpRemaining);
    }

    [Fact]
    public void Recover_DiscardsEnergyAndHealsFully()
    {
        var (_, ctx) = NewContext("Recover", "0");
        ctx.Attacker.AddDamage(60);

        var result = NewPipeline().Execute(ctx, Registry.Find("Recover")!);

        Assert.True(result.Success);
        Assert.Empty(ctx.Attacker.AttachedEnergies);
        Assert.Equal(0, ctx.Attacker.TotalDamage);
    }

    [Fact]
    public void Torment_BlocksOpponentNextAttack()
    {
        var (state, ctx) = NewContext("Torment", "30");

        var result = NewPipeline().Execute(ctx, Registry.Find("Torment")!);

        Assert.True(result.Success);
        Assert.True(state.Players[1].Active!.HasModifier(
            ModifierType.CannotAttack, state.TurnNumber));
    }

    [Fact]
    public void Moonblast_GivesAttackerReductionUntilNextTurn()
    {
        var (state, ctx) = NewContext("Moonblast", "30");

        NewPipeline().Execute(ctx, Registry.Find("Moonblast")!);

        Assert.True(ctx.Attacker.HasModifier(
            ModifierType.DamageReduction, state.TurnNumber));
    }

    [Fact]
    public void Joust_KnocksOffDefenderTool()
    {
        var (state, ctx) = NewContext("Joust", "30");
        ctx.Defender.AttachTool(TestCards.Tool("tool", "Muscle Band"));

        NewPipeline().Execute(ctx, Registry.Find("Joust")!);

        Assert.Null(ctx.Defender.AttachedTool);
        Assert.Contains(state.Players[1].DiscardPile, c => c.Id == "tool");
    }
}
