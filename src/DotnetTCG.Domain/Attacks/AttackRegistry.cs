using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Attacks;

public sealed class AttackRegistry
{
    private readonly Dictionary<string, AttackBehavior> _behaviors;

    public AttackRegistry()
    {
        _behaviors = new Dictionary<string, AttackBehavior>(StringComparer.Ordinal);
        RegisterVariableDamage();
        RegisterComplex();
        RegisterBlockers();
    }

    public AttackBehavior? Find(string attackName)
    {
        if (_behaviors.TryGetValue(attackName, out var behavior))
            return behavior;
        return _behaviors.Values.FirstOrDefault(b =>
            b.Aliases?.Contains(attackName, StringComparer.Ordinal) == true);
    }

    private void Add(string name, AttackBehavior behavior) => _behaviors[name] = behavior;

    private static int CountAttached(PokemonInPlay pokemon, PokemonType? type = null)
        => type is null
            ? pokemon.TotalEnergyCount
            : pokemon.AttachedEnergies.Where(e => e.EnergyType == type).Sum(e => e.EnergyValue);

    private static int FlipCount(ICoinFlipService coin, int flips)
    {
        var heads = 0;
        for (var i = 0; i < flips; i++)
        {
            if (coin.FlipHeads())
                heads++;
        }

        return heads;
    }

    private void RegisterVariableDamage()
    {
        Add("Blaze Ball", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + 20 * CountAttached(ctx.Attacker, PokemonType.Fire)));
        Add("Hydro Pump", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + 20 * CountAttached(ctx.Attacker, PokemonType.Water)));
        Add("Colorful Wind", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + 30 * ctx.Attacker.AttachedEnergies
                .Select(e => e.EnergyType).Distinct().Count()));
        Add("Evil Ball", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage
                + 20 * (ctx.Attacker.TotalEnergyCount + ctx.Defender.TotalEnergyCount)));
        Add("Balloon Barrage", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + 20 * ctx.Attacker.TotalEnergyCount));
        Add("Circle Circuit", new AttackBehavior(
            VariableDamage: ctx =>
            {
                var bench = ctx.State.Players[ctx.ActingPlayerIndex].Bench.Count;
                return ctx.BaseDamage + 20 * bench;
            }));
        Add("Cosmic Spin", new AttackBehavior(
            VariableDamage: ctx =>
            {
                var board = ctx.State.Players[ctx.ActingPlayerIndex];
                var lunatone = (board.Active is not null && board.Active.Card.Name == "Lunatone")
                    || board.Bench.Any(p => p.Card.Name == "Lunatone");
                return ctx.BaseDamage + (lunatone ? 30 : 0);
            }));
        Add("Core Splash", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage
                + (CountAttached(ctx.Attacker, PokemonType.Psychic) > 0 ? 30 : 0)));
        Add("Tailspin", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + (ctx.Defender.TotalDamage > 0 ? 40 : 0)));
        Add("Bite Off", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + (ctx.Defender.Card.IsEx ? 60 : 0)));
        Add("Wake-Up", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + (ctx.Defender.Statuses.Count > 0 ? 60 : 0),
            Aliases: ["Wake-Up Slap"]));
        Add("Rage", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + 10 * ctx.Attacker.DamageCounters));
        Add("Second Bite", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + 10 * ctx.Defender.DamageCounters));
        Add("Leaf Munch", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage
                + (CountAttached(ctx.Attacker, PokemonType.Grass) > 0 ? 20 : 0)));
        Add("Magma Mantle", new AttackBehavior(
            VariableDamage: ctx =>
            {
                var top = ctx.State.Players[ctx.ActingPlayerIndex].PeekTop();
                var bonus = top is EnergyCard { EnergyType: PokemonType.Fire } ? 50 : 0;
                return ctx.BaseDamage + bonus;
            }));
        Add("Continuous Tumble", new AttackBehavior(
            VariableDamage: ctx => FlipCount(ctx.CoinFlip, 4) * 30));
        Add("Spiny Rush", new AttackBehavior(
            VariableDamage: ctx => FlipCount(ctx.CoinFlip, 2) * 20));
        Add("Rock Blast", new AttackBehavior(
            VariableDamage: ctx =>
            {
                var fighting = ctx.Attacker.AttachedEnergies
                    .Where(e => e.EnergyType == PokemonType.Fighting).Sum(e => e.EnergyValue);
                return FlipCount(ctx.CoinFlip, fighting) * 50;
            },
            Aliases: ["Rock Black"]));
        Add("Charge Dash", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + 20,
            PostEffects: [new EffectSpec(EffectType.SelfDamage, Amount: 20)]));
        Add("Electron Crush", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + (ctx.OptionalDiscardAccepted ? 30 : 0),
            NeedsOptionalDiscard: true));
        Add("Seething Anger", new AttackBehavior(
            VariableDamage: ctx => FlipCount(ctx.CoinFlip, ctx.Attacker.DamageCounters) * 30));
        Add("Hyper Fang", new AttackBehavior(FlipToFail: true));
    }

    private void RegisterComplex()
    {
        Add("Rapid Spin", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.SwitchActive, Target: EffectTarget.OwnBench),
            new EffectSpec(EffectType.OpponentSwitch),
        ]));
        Add("Splash Bomb", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.SelfDamage, Amount: 30, ApplyOnTailsOnly: true),
        ]));
        Add("Conversion Powder", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.ApplyStatus, Target: EffectTarget.OpponentActive, Status: Status.Asleep),
            new EffectSpec(EffectType.ApplyStatus, Target: EffectTarget.OpponentActive, Status: Status.Poisoned),
        ]));
        Add("Confusion Wave", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.ApplyStatus, Target: EffectTarget.OpponentActive, Status: Status.Confused),
            new EffectSpec(EffectType.ApplyStatus, Target: EffectTarget.OwnActive, Status: Status.Confused),
        ]));
        Add("Night Claw", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.SelfPlayer, Amount: 2),
        ]));
        Add("Flare Blitz", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.SelfPlayer,
                Amount: int.MaxValue, EnergyType: PokemonType.Fire),
        ]));
        Add("Fire Tail", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.SelfPlayer,
                Amount: 1, EnergyType: PokemonType.Fire),
        ]));
        Add("Flame Charge", new AttackBehavior(Complex: ctx =>
            ctx.Effects.AttachFromDeck(
                new EffectContext(ctx.State, ctx.ActingPlayerIndex, ctx.Attacker),
                PokemonType.Fire, ctx.Attacker)));
        Add("Energy Glide", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.MoveEnergy, EnergyType: PokemonType.Lightning),
            new EffectSpec(EffectType.SwitchActive, Target: EffectTarget.OwnBench),
        ]));
        Add("Oblivion", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.SelfPlayer,
                Amount: 1, EnergyType: PokemonType.Darkness),
        ]));
        Add("Geomancy", new AttackBehavior(Complex: ctx =>
        {
            var board = ctx.State.Players[ctx.ActingPlayerIndex];
            var destinations = board.Bench.Count > 0
                ? board.Bench.Take(2).ToList()
                : [ctx.Attacker];
            var effectCtx = new EffectContext(ctx.State, ctx.ActingPlayerIndex, ctx.Attacker);
            foreach (var destination in destinations)
                ctx.Effects.AttachFromDeck(effectCtx, PokemonType.Fairy, destination);
        }));
        Add("Joust", new AttackBehavior(Complex: ctx =>
        {
            var tool = ctx.Defender.RemoveTool();
            if (tool is not null)
                ctx.State.OpponentOf(ctx.ActingPlayerIndex).Discard(tool);
        }));
        Add("Y Cyclone", new AttackBehavior(Complex: ctx =>
        {
            var opponent = ctx.State.OpponentOf(ctx.ActingPlayerIndex);
            var energy = ctx.Defender.AttachedEnergies.FirstOrDefault();
            var destination = opponent.Bench.FirstOrDefault();
            if (energy is null || destination is null)
                return;
            ctx.Defender.DetachEnergy(energy);
            destination.AttachEnergy(energy);
        }));
        Add("Tricky Steps", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.MoveEnergy),
        ]));
        Add("Energy Salon", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.MoveEnergy),
        ]));
        Add("Spirit Scream", new AttackBehavior(Complex: ctx =>
        {
            ctx.Attacker.ReduceToHp(10);
            ctx.Defender.ReduceToHp(10);
        }));
        Add("Clamp", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.ApplyStatus, Target: EffectTarget.OpponentActive, Status: Status.Paralyzed),
            new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.OpponentActive, Amount: 1),
        ]));
        Add("Bounce", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.SwitchActive, Target: EffectTarget.OwnBench, FlipToActivate: true),
        ]));
        Add("Hypno", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + 30,
            PostEffects:
            [
                new EffectSpec(EffectType.ApplyStatus, Target: EffectTarget.OwnActive, Status: Status.Asleep),
            ],
            Aliases: ["Hypnosis"]));
        Add("Recover", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.SelfPlayer, Amount: 1),
            new EffectSpec(EffectType.HealAll, Target: EffectTarget.AllOwn),
        ]));
        Add("Dig Out", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.Heal, Target: EffectTarget.OwnActive, Amount: 30),
            new EffectSpec(EffectType.Draw, Target: EffectTarget.SelfPlayer, Amount: 1),
        ]));
        Add("Seafaring", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.SearchEnergy, FlipToActivate: true),
        ]));
        Add("Lead Skiddo", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.SearchAny, Target: EffectTarget.SelfPlayer,
                TrainerSubtype: SubType.Supporter),
        ]));
        Add("Clairvoyant", new AttackBehavior());
        Add("Mine", new AttackBehavior());
        Add("Pickup", new AttackBehavior(Complex: ctx =>
        {
            var board = ctx.State.Players[ctx.ActingPlayerIndex];
            board.TakeFromDiscard(c => c is TrainerCard t && t.TrainerSubtype == SubType.Item, 2);
        }));
        Add("Bother", new AttackBehavior(Complex: ctx =>
            ctx.State.BlockSupporters(1 - ctx.ActingPlayerIndex, ctx.State.TurnNumber + 1)));
        Add("Moonblast", new AttackBehavior(Complex: ctx =>
            ctx.Attacker.AddModifier(new BoardModifier(ModifierType.DamageReduction, "Moonblast",
                20, ModifierDuration.UntilNextTurn, ctx.State.TurnNumber))));
        Add("Metal", new AttackBehavior(Complex: ctx =>
            ctx.Attacker.AddModifier(new BoardModifier(ModifierType.DamageBoost, "Metal",
                40, ModifierDuration.UntilNextTurn, ctx.State.TurnNumber))));
        Add("Astonish", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.ShuffleHand, Target: EffectTarget.OpponentPlayer),
        ]));
        Add("Mad Mountain", new AttackBehavior(Complex: ctx =>
            ctx.State.OpponentOf(ctx.ActingPlayerIndex).MillTop(ctx.Attacker.DamageCounters)));
    }

    private static readonly IReadOnlyList<EffectSpec> BlockAttackEffects =
    [
        new EffectSpec(EffectType.RestrictAttack, Target: EffectTarget.OpponentActive),
    ];

    private void RegisterBlockers()
    {
        Add("Torment", new AttackBehavior(PostEffects: BlockAttackEffects));
        Add("Darkness Fang", new AttackBehavior(
            PostEffects: BlockAttackEffects, Aliases: ["Darkness"]));
        Add("King's Smash", new AttackBehavior(
            PostEffects: BlockAttackEffects, Aliases: ["King's"]));
        Add("X Scissor", new AttackBehavior(
            PostEffects: BlockAttackEffects, Aliases: ["X"]));
        Add("Rock Throw", new AttackBehavior(
            PostEffects: BlockAttackEffects, Aliases: ["Rock"]));
    }
}
