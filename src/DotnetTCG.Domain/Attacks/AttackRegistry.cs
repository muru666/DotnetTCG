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
        RegisterXy1Effects();
    }

    /// <summary>Nombres clave del registry (para tests de cobertura contra data/xy1.json).</summary>
    public IReadOnlyCollection<string> RegisteredNames => _behaviors.Keys;

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
        // AUDIT D011: la carta real se llama "Tailspin Piledriver" (xy1); "Tailspin" queda como alias.
        Add("Tailspin Piledriver", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + (ctx.Defender.TotalDamage > 0 ? 40 : 0),
            Aliases: ["Tailspin"]));
        Add("Bite Off", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + (ctx.Defender.Card.IsEx ? 60 : 0)));
        // AUDIT D011: la carta real se llama "Wake-Up Slap" (xy1); "Wake-Up" queda como alias.
        // AUDIT D011: el texto además remueve los Special Conditions del defensor después del
        // daño (no está implementado) — pendiente de decisión.
        Add("Wake-Up Slap", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + (ctx.Defender.Statuses.Count > 0 ? 60 : 0),
            Aliases: ["Wake-Up"]));
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
        // AUDIT D011: la carta real se llama "Rock Black" (xy1); "Rock Blast" queda como alias.
        Add("Rock Black", new AttackBehavior(
            VariableDamage: ctx =>
            {
                var fighting = ctx.Attacker.AttachedEnergies
                    .Where(e => e.EnergyType == PokemonType.Fighting).Sum(e => e.EnergyValue);
                return FlipCount(ctx.CoinFlip, fighting) * 50;
            },
            Aliases: ["Rock Blast"]));
        Add("Charge Dash", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + 20,
            PostEffects: [new EffectSpec(EffectType.SelfDamage, Amount: 20)]));
        Add("Electron Crush", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + (ctx.OptionalDiscardAccepted ? 30 : 0),
            NeedsOptionalDiscard: true));
        Add("Seething Anger", new AttackBehavior(
            VariableDamage: ctx => FlipCount(ctx.CoinFlip, ctx.Attacker.DamageCounters) * 30));

        // AUDIT D011: ataques con daño variable de xy1 (damage "N×" / "N+") que no tenían
        // behavior y se resolvían con el daño fijo. Fórmulas según el campo text de data/xy1.json.
        Add("Pin Missile", new AttackBehavior(
            VariableDamage: ctx => FlipCount(ctx.CoinFlip, 4) * 10));
        Add("Spike Cannon", new AttackBehavior(
            VariableDamage: ctx => FlipCount(ctx.CoinFlip, 5) * 30));
        Add("Double Headbutt", new AttackBehavior(
            VariableDamage: ctx => FlipCount(ctx.CoinFlip, 2) * 30));
        Add("Double Hit", new AttackBehavior(
            VariableDamage: ctx => FlipCount(ctx.CoinFlip, 2) * 30));
        Add("Dual Blades", new AttackBehavior(
            VariableDamage: ctx => FlipCount(ctx.CoinFlip, 2) * 30));
        // AUDIT D011: el texto también previene todos los efectos si salen 3 caras; requiere
        // compartir el flip del daño con un PostEffect — pendiente.
        Add("Flash Needle", new AttackBehavior(
            VariableDamage: ctx => FlipCount(ctx.CoinFlip, 3) * 40));
        Add("Aerial Ace", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + FlipCount(ctx.CoinFlip, 1) * 30));
        // AUDIT D011: xy1 tiene dos cartas "Quick Attack" con distinto bonus (Illumise +20,
        // Pikachu +10); se distingue por el nombre de la carta (mismo patrón que Cosmic Spin).
        Add("Quick Attack", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage
                + FlipCount(ctx.CoinFlip, 1) * (ctx.Attacker.Card.Name == "Illumise" ? 20 : 10)));
        // AUDIT D011: si sale cara el texto además confunde al defensor; requiere compartir el
        // flip del daño con un PostEffect — pendiente.
        Add("Dynamic Punch", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + FlipCount(ctx.CoinFlip, 1) * 40));
        Add("Endeavor", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + FlipCount(ctx.CoinFlip, 2) * 20));
        Add("Jump On", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + FlipCount(ctx.CoinFlip, 1) * 20));
        Add("Pummel", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + FlipCount(ctx.CoinFlip, 1) * 20));
        Add("Random Peck", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + FlipCount(ctx.CoinFlip, 2) * 20));
        Add("Slap Down", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + FlipCount(ctx.CoinFlip, 2) * 20));
        Add("Water Splash", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + FlipCount(ctx.CoinFlip, 1) * 20));

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
        // AUDIT D011: efecto divergente del texto de carta — pendiente de decisión
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
        // AUDIT D011: efecto divergente del texto de carta — pendiente de decisión
        Add("Night Claw", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.SelfPlayer, Amount: 2),
        ]));
        Add("Flare Blitz", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.SelfPlayer,
                Amount: int.MaxValue, EnergyType: PokemonType.Fire),
        ]));
        // AUDIT D011: la carta real se llama "Fire Tail Slap" (xy1); "Fire Tail" queda como alias.
        Add("Fire Tail Slap", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.SelfPlayer,
                Amount: 1, EnergyType: PokemonType.Fire),
        ], Aliases: ["Fire Tail"]));
        Add("Flame Charge", new AttackBehavior(Complex: ctx =>
            ctx.Effects.AttachFromDeck(
                new EffectContext(ctx.State, ctx.ActingPlayerIndex, ctx.Attacker),
                PokemonType.Fire, ctx.Attacker)));
        Add("Energy Glide", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.MoveEnergy, EnergyType: PokemonType.Lightning),
            new EffectSpec(EffectType.SwitchActive, Target: EffectTarget.OwnBench),
        ]));
        // AUDIT D011: la carta real se llama "Oblivion Wing" (xy1); "Oblivion" queda como alias.
        // AUDIT D011: el texto dice "adjunta una Energía Darkness de tu descarte a tu banca" y el
        // behavior descarta 1 Darkness del activo — pendiente de decisión.
        Add("Oblivion Wing", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.SelfPlayer,
                Amount: 1, EnergyType: PokemonType.Darkness),
        ], Aliases: ["Oblivion"]));
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
        // AUDIT D011: efecto divergente del texto de carta (intercambiado con Tricky Steps) —
        // pendiente de decisión
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
        // AUDIT D011: efecto divergente del texto de carta (intercambiado con Y Cyclone) —
        // pendiente de decisión
        Add("Tricky Steps", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.MoveEnergy),
        ]));
        // AUDIT D011: efecto divergente del texto de carta — pendiente de decisión
        Add("Energy Salon", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.MoveEnergy),
        ]));
        Add("Spirit Scream", new AttackBehavior(Complex: ctx =>
        {
            ctx.Attacker.ReduceToHp(10);
            ctx.Defender.ReduceToHp(10);
        }));
        // AUDIT D011: la carta real se llama "Clamp Crush" (xy1); "Clamp" queda como alias.
        // AUDIT D011: el texto exige flip ("si sale cara…"); el behavior lo aplica siempre —
        // pendiente de decisión.
        Add("Clamp Crush", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.ApplyStatus, Target: EffectTarget.OpponentActive, Status: Status.Paralyzed),
            new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.OpponentActive, Amount: 1),
        ], Aliases: ["Clamp"]));
        Add("Bounce", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.SwitchActive, Target: EffectTarget.OwnBench, FlipToActivate: true),
        ]));
        // AUDIT D011: la carta real se llama "Hypno Headbutt" (xy1); "Hypno"/"Hypnosis" quedan
        // como alias. AUDIT D011: el texto es "puedes hacer 30 más; si lo haces, este Pokémon
        // queda Dormido" (opcional) y el behavior lo aplica siempre — pendiente de decisión.
        Add("Hypno Headbutt", new AttackBehavior(
            VariableDamage: ctx => ctx.BaseDamage + 30,
            PostEffects:
            [
                new EffectSpec(EffectType.ApplyStatus, Target: EffectTarget.OwnActive, Status: Status.Asleep),
            ],
            Aliases: ["Hypno", "Hypnosis"]));
        // AUDIT D011: efecto divergente del texto de carta — pendiente de decisión
        Add("Recover", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.SelfPlayer, Amount: 1),
            new EffectSpec(EffectType.HealAll, Target: EffectTarget.AllOwn),
        ]));
        // AUDIT D011: efecto divergente del texto de carta — pendiente de decisión
        Add("Dig Out", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.Heal, Target: EffectTarget.OwnActive, Amount: 30),
            new EffectSpec(EffectType.Draw, Target: EffectTarget.SelfPlayer, Amount: 1),
        ]));
        Add("Seafaring", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.SearchEnergy, FlipToActivate: true),
        ]));
        // AUDIT D011: la carta real se llama "Lead" (xy1); "Lead Skiddo" queda como alias.
        // AUDIT D011: xy1 tiene dos cartas "Lead" (Skiddo: flip → 1 Supporter; Gogoat: hasta 2
        // sin flip) y el behavior busca 1 sin flip — pendiente de decisión.
        Add("Lead", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.SearchAny, Target: EffectTarget.SelfPlayer,
                TrainerSubtype: SubType.Supporter),
        ], Aliases: ["Lead Skiddo"]));
        // AUDIT D011: la carta real se llama "Clairvoyant Eye" (xy1); "Clairvoyant" queda como
        // alias. AUDIT D011: el texto ("mira las 3 primeras cartas de tu mazo y devuélvelas en
        // cualquier orden") no está implementado — pendiente.
        Add("Clairvoyant Eye", new AttackBehavior(Aliases: ["Clairvoyant"]));
        // AUDIT D011: el texto ("mira la primera carta del mazo rival; puedes barajarlo") no
        // está implementado — pendiente.
        Add("Mine", new AttackBehavior());
        Add("Pickup", new AttackBehavior(Complex: ctx =>
        {
            var board = ctx.State.Players[ctx.ActingPlayerIndex];
            board.TakeFromDiscard(c => c is TrainerCard t && t.TrainerSubtype == SubType.Item, 2);
        }));
        // AUDIT D011: efecto divergente del texto de carta — pendiente de decisión
        Add("Bother", new AttackBehavior(Complex: ctx =>
            ctx.State.BlockSupporters(1 - ctx.ActingPlayerIndex, ctx.State.TurnNumber + 1)));
        Add("Moonblast", new AttackBehavior(Complex: ctx =>
            ctx.Attacker.AddModifier(new BoardModifier(ModifierType.DamageReduction, "Moonblast",
                20, ModifierDuration.UntilNextTurn, ctx.State.TurnNumber))));
        // AUDIT D011: clave muerta — ningún ataque de xy1 se llama "Metal" (verificado contra
        // data/xy1.json, 146 cartas); se conserva por los tests existentes.
        Add("Metal", new AttackBehavior(Complex: ctx =>
            ctx.Attacker.AddModifier(new BoardModifier(ModifierType.DamageBoost, "Metal",
                40, ModifierDuration.UntilNextTurn, ctx.State.TurnNumber))));
        // AUDIT D011: efecto divergente del texto de carta — pendiente de decisión
        Add("Astonish", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.ShuffleHand, Target: EffectTarget.OpponentPlayer),
        ]));
        // AUDIT D011: efecto divergente del texto de carta — pendiente de decisión
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
        // "Darkness" no existe como ataque en xy1; alias histórico de "Darkness Fang".
        Add("Darkness Fang", new AttackBehavior(
            PostEffects: BlockAttackEffects, Aliases: ["Darkness"]));
        // AUDIT D011: clave muerta — ningún ataque de xy1 se llama "King's Smash" (verificado
        // contra data/xy1.json, 146 cartas); se conserva por los tests existentes.
        Add("King's Smash", new AttackBehavior(
            PostEffects: BlockAttackEffects, Aliases: ["King's"]));
        // AUDIT D011: clave muerta — ningún ataque de xy1 se llama "X Scissor" (verificado
        // contra data/xy1.json, 146 cartas); se conserva por los tests existentes.
        Add("X Scissor", new AttackBehavior(
            PostEffects: BlockAttackEffects, Aliases: ["X"]));
        // AUDIT D011: clave muerta — ningún ataque de xy1 se llama "Rock Throw" (verificado
        // contra data/xy1.json, 146 cartas); se conserva por los tests existentes.
        Add("Rock Throw", new AttackBehavior(
            PostEffects: BlockAttackEffects, Aliases: ["Rock"]));
    }

    /// <summary>
    /// AUDIT D011: efectos de ataques de xy1 que no tenían behavior. Cada entrada mapea el
    /// campo text de la carta sobre los EffectSpec ya existentes. Los ataques cuyo efecto no
    /// tiene mapeo correcto (elección de destino, flip compartido, mecanismo ausente) quedan
    /// en KnownGaps del test AttackRegistryXy1CoverageTests, no acá.
    /// </summary>
    private void RegisterXy1Effects()
    {
        // --- Daños a la banca rival (NeedsBenchTarget resuelve el objetivo elegido) ---
        Add("Break Through", new AttackBehavior(
            NeedsBenchTarget: EffectTarget.OpponentBench,
            PostEffects: [new EffectSpec(EffectType.PutCounters, Target: EffectTarget.OpponentBench, Amount: 3)]));
        Add("Mach Punch", new AttackBehavior(
            NeedsBenchTarget: EffectTarget.OpponentBench,
            PostEffects: [new EffectSpec(EffectType.PutCounters, Target: EffectTarget.OpponentBench, Amount: 1)]));
        Add("Wild Barking", new AttackBehavior(
            NeedsBenchTarget: EffectTarget.OpponentBench,
            PostEffects: [new EffectSpec(EffectType.PutCounters, Target: EffectTarget.OpponentBench, Amount: 2)]));
        Add("Eerie Voice", new AttackBehavior(PostEffects:
            [new EffectSpec(EffectType.PutCounters, Target: EffectTarget.AllOpponents, Amount: 2)]));
        // Daños propios
        Add("Earthquake", new AttackBehavior(PostEffects:
            [new EffectSpec(EffectType.PutCounters, Target: EffectTarget.OwnBench, Amount: 1)]));
        Add("Double-Edge", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.SelfDamage, Amount: 10)]));
        Add("Reckless Charge", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.SelfDamage, Amount: 10)]));
        Add("Take Down", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.SelfDamage, Amount: 10)]));
        Add("Wood Hammer", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.SelfDamage, Amount: 10)]));

        // --- Curas ---
        Add("Draining Kiss", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.Heal, Target: EffectTarget.OwnActive, Amount: 30)]));
        Add("Jungle Hammer", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.Heal, Target: EffectTarget.OwnActive, Amount: 30)]));
        Add("Leech Seed", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.Heal, Target: EffectTarget.OwnActive, Amount: 10)]));
        Add("Touchdown", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.Heal, Target: EffectTarget.OwnActive, Amount: 20)]));
        Add("Heal Bell", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.Heal, Target: EffectTarget.AllOwn, Amount: 10)]));
        Add("Refresh", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.Heal, Target: EffectTarget.OwnActive, Amount: 30),
            new EffectSpec(EffectType.RemoveStatus, Target: EffectTarget.OwnActive),
        ]));
        Add("Massage", new AttackBehavior(
            NeedsBenchTarget: EffectTarget.OwnBench,
            PostEffects: [new EffectSpec(EffectType.Heal, Target: EffectTarget.OwnBench, Amount: 60)]));

        // --- Estados especiales ---
        Add("Confuse Ray", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.ApplyStatus, Target: EffectTarget.OpponentActive, Status: Status.Confused)]));
        Add("Metal Sound", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.ApplyStatus, Target: EffectTarget.OpponentActive, Status: Status.Confused)]));
        Add("Psybeam", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.ApplyStatus, Target: EffectTarget.OpponentActive, Status: Status.Confused)]));
        Add("Signal Beam", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.ApplyStatus, Target: EffectTarget.OpponentActive, Status: Status.Confused)]));
        Add("Yawn", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.ApplyStatus, Target: EffectTarget.OpponentActive, Status: Status.Asleep)]));
        Add("Poison Jab", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.ApplyStatus, Target: EffectTarget.OpponentActive, Status: Status.Poisoned)]));
        Add("Poison Powder", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.ApplyStatus, Target: EffectTarget.OpponentActive, Status: Status.Poisoned)]));
        Add("Poison Sting", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.ApplyStatus, Target: EffectTarget.OpponentActive, Status: Status.Poisoned)]));
        Add("Poison Ring", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.ApplyStatus, Target: EffectTarget.OpponentActive, Status: Status.Poisoned),
            new EffectSpec(EffectType.PreventRetreat, Target: EffectTarget.OpponentActive),
        ]));
        Add("Crisis Vine", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.ApplyStatus, Target: EffectTarget.OpponentActive, Status: Status.Paralyzed),
            new EffectSpec(EffectType.ApplyStatus, Target: EffectTarget.OpponentActive, Status: Status.Poisoned),
        ]));
        // Flip: si sale cara aplica el estado (el flip es sólo del efecto, no del daño).
        Add("Body Slam", new AttackBehavior(PostEffects:
            [new EffectSpec(EffectType.FlipStatus, Target: EffectTarget.OpponentActive, Status: Status.Paralyzed)]));
        Add("Fake Out", new AttackBehavior(PostEffects:
            [new EffectSpec(EffectType.FlipStatus, Target: EffectTarget.OpponentActive, Status: Status.Paralyzed)]));
        Add("Glare", new AttackBehavior(PostEffects:
            [new EffectSpec(EffectType.FlipStatus, Target: EffectTarget.OpponentActive, Status: Status.Paralyzed)]));
        Add("Lick", new AttackBehavior(PostEffects:
            [new EffectSpec(EffectType.FlipStatus, Target: EffectTarget.OpponentActive, Status: Status.Paralyzed)]));
        Add("Nuzzle", new AttackBehavior(PostEffects:
            [new EffectSpec(EffectType.FlipStatus, Target: EffectTarget.OpponentActive, Status: Status.Paralyzed)]));
        Add("Stun Spore", new AttackBehavior(PostEffects:
            [new EffectSpec(EffectType.FlipStatus, Target: EffectTarget.OpponentActive, Status: Status.Paralyzed)]));

        // --- Descartes ---
        Add("Crunch", new AttackBehavior(PostEffects:
            [new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.OpponentActive, Amount: 1, FlipToActivate: true)]));
        Add("Cut Down", new AttackBehavior(PostEffects:
            [new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.OpponentActive, Amount: 1, FlipToActivate: true)]));
        Add("Energy Cutoff", new AttackBehavior(PostEffects:
            [new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.OpponentActive, Amount: 1, FlipToActivate: true)]));
        Add("Rip Claw", new AttackBehavior(PostEffects:
            [new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.OpponentActive, Amount: 1, FlipToActivate: true)]));
        Add("Heartfelt Song", new AttackBehavior(PostEffects:
            [new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.OpponentActive, Amount: 1, EnergyType: PokemonType.Darkness)]));
        Add("Fireworks", new AttackBehavior(PostEffects:
            [new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.SelfPlayer, Amount: 1)]));
        Add("Rainbow Spear", new AttackBehavior(PostEffects:
            [new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.SelfPlayer, Amount: 1)]));
        // AUDIT D011: Slugma (50) dice "una Energía" y Simisear (90) "una Energía Fire"; con el
        // coste [Fire,C,C] siempre hay una Energía Fire adjunta, así que se descarta esa.
        Add("Flamethrower", new AttackBehavior(PostEffects:
            [new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.SelfPlayer, Amount: 1, EnergyType: PokemonType.Fire)]));
        Add("Thunderbolt", new AttackBehavior(PostEffects:
            [new EffectSpec(EffectType.DiscardAll, Target: EffectTarget.SelfPlayer)]));
        Add("Hammer Arm", new AttackBehavior(PostEffects:
            [new EffectSpec(EffectType.DiscardOpponentDeck, Amount: 1)]));

        // --- Robos / mazo ---
        Add("Double Draw", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.Draw, Target: EffectTarget.SelfPlayer, Amount: 2)]));
        Add("Filch", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.Draw, Target: EffectTarget.SelfPlayer, Amount: 1)]));
        Add("Me First", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.Draw, Target: EffectTarget.SelfPlayer, Amount: 1)]));
        Add("Devastating Wind", new AttackBehavior(PostEffects:
        [
            new EffectSpec(EffectType.ShuffleHand, Target: EffectTarget.OpponentPlayer),
            new EffectSpec(EffectType.Draw, Target: EffectTarget.OpponentPlayer, Amount: 4),
        ]));
        Add("Nasty Plot", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.SearchAny, Target: EffectTarget.SelfPlayer)]));
        // AUDIT D011: EffectType.Recycle hoy deja la carta en la mano (PlayerBoard.RecycleFirst)
        // y el texto pide "encima de tu mazo" — pendiente de decisión (EffectEngine es ajeno
        // al alcance de D011).
        Add("Recycle", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.Recycle, Target: EffectTarget.SelfPlayer)]));

        // --- Cambios de activo ---
        Add("Knock Back", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.OpponentSwitch)]));
        Add("Luring Glow", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.OpponentSwitch)]));

        // --- Restricciones / prevención ---
        Add("Corner", new AttackBehavior(
            PostEffects: [new EffectSpec(EffectType.PreventRetreat, Target: EffectTarget.OpponentActive)]));
        Add("Hocus Pinkus", new AttackBehavior(PostEffects: BlockAttackEffects));
        Add("Darkness Blade", new AttackBehavior(PostEffects:
            [new EffectSpec(EffectType.RestrictAttack, Target: EffectTarget.OwnActive, ApplyOnTailsOnly: true)]));
        Add("Rock Wrecker", new AttackBehavior(PostEffects:
            [new EffectSpec(EffectType.RestrictAttack, Target: EffectTarget.OwnActive)]));
        Add("Scrunch", new AttackBehavior(PostEffects:
            [new EffectSpec(EffectType.Prevent, Target: EffectTarget.OwnActive, FlipToActivate: true)]));
    }
}
