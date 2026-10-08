using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Tests;

public sealed class EffectEngineTests
{
    private readonly StatusEffectManager _statuses = new();

    private EffectEngine Engine(ICoinFlipService? coin = null)
        => new(_statuses, coin ?? new SeededCoinFlip(), new Random(11));

    private static GameState NewBattleState()
    {
        var state = new GameState(new PlayerBoard(), new PlayerBoard(), 0, suddenDeath: false);
        state.SetStatus(GameStatus.InProgress);
        foreach (var board in state.Players)
        {
            board.LoadDeckForTests(TestHelpers.DeckWithBasics(60));
            board.PlaceActive(new PokemonInPlay(TestCards.BasicPokemon("a", "A", hp: 100)));
        }
        return state;
    }

    private static EffectContext Ctx(GameState state, int acting = 0)
        => new(state, acting, Source: state.Players[acting].Active);

    [Fact]
    public void ApplyStatus_BurnsOpponentActive()
    {
        var state = NewBattleState();

        Engine().Apply(Ctx(state), new EffectSpec(EffectType.ApplyStatus,
            Target: EffectTarget.OpponentActive, Status: Status.Burned));

        Assert.True(state.Players[1].Active!.HasStatus(Status.Burned));
    }

    [Fact]
    public void Heal_RestoresHp()
    {
        var state = NewBattleState();
        state.Players[1].Active!.AddDamage(50);

        Engine().Apply(Ctx(state), new EffectSpec(EffectType.Heal,
            Target: EffectTarget.OpponentActive, Amount: 30));

        Assert.Equal(20, state.Players[1].Active.TotalDamage);
    }

    [Fact]
    public void SelfDamage_HurtsSource()
    {
        var state = NewBattleState();

        Engine().Apply(Ctx(state), new EffectSpec(EffectType.SelfDamage, Amount: 30));

        Assert.Equal(30, state.Players[0].Active!.TotalDamage);
    }

    [Fact]
    public void Draw_AddsCardsToHand()
    {
        var state = NewBattleState();

        Engine().Apply(Ctx(state), new EffectSpec(EffectType.Draw, Amount: 3));

        Assert.Equal(3, state.Players[0].Hand.Count);
    }

    [Fact]
    public void DiscardEnergy_RemovesOne()
    {
        var state = NewBattleState();
        var active = state.Players[1].Active!;
        active.AttachEnergy(TestCards.BasicEnergy("e1", PokemonType.Fire));
        active.AttachEnergy(TestCards.BasicEnergy("e2", PokemonType.Fire));

        Engine().Apply(Ctx(state), new EffectSpec(EffectType.DiscardEnergy,
            Target: EffectTarget.OpponentActive, Amount: 1, EnergyType: PokemonType.Fire));

        Assert.Single(active.AttachedEnergies);
    }

    [Fact]
    public void DiscardOpponentHand_LeavesN()
    {
        var state = NewBattleState();
        for (var i = 0; i < 5; i++)
            state.Players[1].AddToHand(TestCards.BasicEnergy($"h{i}", PokemonType.Fire));

        Engine().Apply(Ctx(state), new EffectSpec(EffectType.DiscardOpponentHand, Amount: 2));

        Assert.Equal(2, state.Players[1].Hand.Count);
        Assert.Equal(3, state.Players[1].DiscardPile.Count);
    }

    [Fact]
    public void SearchEnergy_TakesFromDeckToHand()
    {
        var state = NewBattleState();

        Engine().Apply(Ctx(state), new EffectSpec(EffectType.SearchEnergy,
            EnergyType: PokemonType.Colorless));

        Assert.Single(state.Players[0].Hand);
    }

    [Fact]
    public void PutCounters_DealsTenPerCounter()
    {
        var state = NewBattleState();

        Engine().Apply(Ctx(state), new EffectSpec(EffectType.PutCounters,
            Target: EffectTarget.OpponentActive, Amount: 3));

        Assert.Equal(30, state.Players[1].Active!.TotalDamage);
    }

    [Fact]
    public void DrawUntil_FillsHandToAmount()
    {
        var state = NewBattleState();
        state.Players[0].AddToHand(TestCards.BasicEnergy("h", PokemonType.Fire));

        Engine().Apply(Ctx(state), new EffectSpec(EffectType.DrawUntil, Amount: 4));

        Assert.Equal(4, state.Players[0].Hand.Count);
    }

    [Fact]
    public void MoveEnergy_TransfersToBench()
    {
        var state = NewBattleState();
        var bench = new PokemonInPlay(TestCards.BasicPokemon("b", "B", hp: 60));
        state.Players[0].AddToBench(bench);
        state.Players[0].Active!.AttachEnergy(TestCards.BasicEnergy("e1", PokemonType.Fairy));

        Engine().Apply(new EffectContext(state, 0, state.Players[0].Active, bench),
            new EffectSpec(EffectType.MoveEnergy, EnergyType: PokemonType.Fairy));

        Assert.Empty(state.Players[0].Active.AttachedEnergies);
        Assert.Single(bench.AttachedEnergies);
    }

    [Fact]
    public void OpponentSwitch_SwapsWithBenchZero()
    {
        var state = NewBattleState();
        var bench = new PokemonInPlay(TestCards.BasicPokemon("b", "B", hp: 60));
        state.Players[1].AddToBench(bench);

        Engine().Apply(Ctx(state), new EffectSpec(EffectType.OpponentSwitch));

        Assert.Same(bench, state.Players[1].Active);
    }

    [Fact]
    public void RemoveStatus_ClearsTarget()
    {
        var state = NewBattleState();
        state.Players[1].Active!.AddStatus(Status.Poisoned);

        Engine().Apply(Ctx(state), new EffectSpec(EffectType.RemoveStatus,
            Target: EffectTarget.OpponentActive));

        Assert.Empty(state.Players[1].Active.Statuses);
    }

    [Fact]
    public void RestrictItems_BlocksOpponentItems()
    {
        var state = NewBattleState();
        var rules = new RuleValidator();

        Engine().Apply(Ctx(state), new EffectSpec(EffectType.RestrictItems,
            Target: EffectTarget.OpponentPlayer));

        Assert.NotNull(rules.CanPlayTrainer(state, 1, TestCards.Item("i", "Potion")));
    }

    [Fact]
    public void FlipStatus_Heads_Applies()
    {
        var state = NewBattleState();

        Engine(new SeededCoinFlip(true)).Apply(Ctx(state), new EffectSpec(EffectType.FlipStatus,
            Target: EffectTarget.OpponentActive, Status: Status.Asleep));

        Assert.True(state.Players[1].Active!.HasStatus(Status.Asleep));
    }

    [Fact]
    public void FlipStatus_Tails_Skips()
    {
        var state = NewBattleState();

        Engine(new SeededCoinFlip(false)).Apply(Ctx(state), new EffectSpec(EffectType.FlipStatus,
            Target: EffectTarget.OpponentActive, Status: Status.Asleep));

        Assert.False(state.Players[1].Active!.HasStatus(Status.Asleep));
    }

    [Fact]
    public void FlipToActivate_Tails_SkipsWholeEffect()
    {
        var state = NewBattleState();

        Engine(new SeededCoinFlip(false)).Apply(Ctx(state), new EffectSpec(EffectType.Draw,
            Amount: 3, FlipToActivate: true));

        Assert.Empty(state.Players[0].Hand);
    }

    [Fact]
    public void Recycle_ReturnsFirstDiscardToHand()
    {
        var state = NewBattleState();
        var card = TestCards.BasicEnergy("d", PokemonType.Fire);
        state.Players[0].Discard(card);

        Engine().Apply(Ctx(state), new EffectSpec(EffectType.Recycle));

        Assert.Contains(card, state.Players[0].Hand);
    }

    [Fact]
    public void Prevent_AddsMassiveReduction()
    {
        var state = NewBattleState();
        var calc = new DamageCalculator();

        Engine().Apply(new EffectContext(state, 1, state.Players[1].Active),
            new EffectSpec(EffectType.Prevent, Target: EffectTarget.OwnActive));
        var damage = calc.Calculate(new DamageCalculationInput(
            100, "Hit", state.Players[0].Active!, state.Players[1].Active!, null));

        Assert.Equal(0, damage);
    }

    [Fact]
    public void HealBench_HealsOnlyBench()
    {
        var state = NewBattleState();
        state.Players[0].Active!.AddDamage(40);
        var bench = new PokemonInPlay(TestCards.BasicPokemon("b", "B", hp: 60));
        bench.AddDamage(40);
        state.Players[0].AddToBench(bench);

        Engine().Apply(Ctx(state), new EffectSpec(EffectType.HealBench, Amount: 30));

        Assert.Equal(40, state.Players[0].Active.TotalDamage);
        Assert.Equal(10, bench.TotalDamage);
    }

    [Fact]
    public void SwitchActive_SwapsWithSelectedBench()
    {
        var state = NewBattleState();
        var bench = new PokemonInPlay(TestCards.BasicPokemon("b", "B", hp: 60));
        state.Players[0].AddToBench(bench);
        var previous = state.Players[0].Active!;

        Engine().Apply(new EffectContext(state, 0, previous, bench),
            new EffectSpec(EffectType.SwitchActive));

        Assert.Same(bench, state.Players[0].Active);
        Assert.Contains(previous, state.Players[0].Bench);
    }
}
