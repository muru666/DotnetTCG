using DotnetTCG.Domain.Attacks;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Attacks;

public sealed class AttackContext
{
    public required GameState State { get; init; }
    public required int ActingPlayerIndex { get; init; }
    public required Attack Attack { get; init; }
    public required PokemonInPlay Attacker { get; init; }
    public required PokemonInPlay Defender { get; init; }
    public int BaseDamage { get; set; }
    public int Damage { get; set; }
    public string? FailureReason { get; set; }
    public PokemonInPlay? SelectedTarget { get; set; }
    public bool? OptionalDiscardAccepted { get; set; }
    public List<EnergyCard> EnergiesToDiscard { get; } = [];
}

public sealed record AttackResult(
    bool Success,
    string? FailureReason,
    int DamageDealt,
    PendingChoice? Pending);

public sealed class AttackPipeline(
    DamageCalculator damageCalculator,
    EffectEngine effects,
    StatusEffectManager statuses,
    ICoinFlipService coinFlip)
{
    public const string PendingBenchTarget = "bench-target";
    public const string PendingOptionalDiscard = "optional-discard";

    public AttackResult Execute(AttackContext context, AttackBehavior? behavior)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.BaseDamage = ParseBaseDamage(context.Attack.Damage);
        context.Damage = behavior?.VariableDamage?.Invoke(new VariableDamageContext(
            context.State,
            context.ActingPlayerIndex,
            context.Attacker,
            context.Defender,
            context.BaseDamage,
            coinFlip,
            new Random(),
            context.OptionalDiscardAccepted == true,
            context.EnergiesToDiscard)) ?? context.BaseDamage;

        if (behavior?.FlipToFail == true && !coinFlip.FlipHeads())
            return Fail(context, "Attack.FlipFail");

        if (context.Attacker.TotalEnergyCount < context.Attack.Cost.Count)
            return Fail(context, "Attack.EnergyShort");

        if (context.Attacker.HasStatus(Status.Confused) && !coinFlip.FlipHeads())
        {
            context.Attacker.AddDamage(StatusEffectManager.ConfusionSelfDamage);
            return Fail(context, "Attack.ConfusedSelfHit", StatusEffectManager.ConfusionSelfDamage);
        }

        return ContinueAfterPreChecks(context, behavior);
    }

    public AttackResult Resume(AttackContext context, AttackBehavior? behavior)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ContinueAfterPreChecks(context, behavior);
    }

    private AttackResult ContinueAfterPreChecks(AttackContext context, AttackBehavior? behavior)
    {
        var pending = CheckSelections(context, behavior);
        if (pending is not null)
            return new AttackResult(false, null, 0, pending);

        if (context.Attacker.HasStatus(Status.Asleep) || context.Attacker.HasStatus(Status.Paralyzed))
            return Fail(context, "Attack.StatusBlocked");

        if (context.Attacker.CannotAttackUntilTurn >= context.State.TurnNumber)
            return Fail(context, "Attack.Blocked");

        if (context.Attacker.HasModifier(ModifierType.CannotAttack, context.State.TurnNumber))
            return Fail(context, "Attack.Blocked");

        DiscardOptionalCost(context, behavior);

        var input = new DamageCalculationInput(
            context.Damage,
            context.Attack.Name,
            context.Attacker,
            context.Defender,
            context.State.StadiumCard);
        var damage = damageCalculator.Calculate(input);
        context.Damage = damage;
        context.Defender.AddDamage(damage);

        if (context.Defender.Card.Abilities.Any(a => a.Name == "Spiky Shield"))
            context.Attacker.AddDamage(30);

        if (behavior?.PostEffects is not null)
        {
            var effectContext = new EffectContext(
                context.State, context.ActingPlayerIndex, context.Attacker, context.SelectedTarget);
            foreach (var spec in behavior.PostEffects)
                effects.Apply(effectContext, spec);
        }

        behavior?.Complex?.Invoke(new ComplexContext(
            context.State,
            context.ActingPlayerIndex,
            context.Attacker,
            context.Defender,
            context.SelectedTarget,
            effects,
            statuses,
            coinFlip,
            new Random()));

        return new AttackResult(true, null, damage, null);
    }

    private PendingChoice? CheckSelections(AttackContext context, AttackBehavior? behavior)
    {
        if (behavior?.NeedsBenchTarget is not null && context.SelectedTarget is null)
        {
            var board = behavior.NeedsBenchTarget == EffectTarget.OwnBench
                ? context.State.Players[context.ActingPlayerIndex]
                : context.State.OpponentOf(context.ActingPlayerIndex);
            if (board.Bench.Count == 0)
                return null;
            return new PendingChoice(
                $"attack:{context.Attack.Name}:{PendingBenchTarget}",
                context.ActingPlayerIndex,
                AbilityName: null,
                "Choose a benched Pokemon.",
                board.Bench.Select((p, i) => new ChoiceOption(i.ToString(), p.Card.Name)).ToList(),
                context.Attack.Name);
        }

        if (behavior?.NeedsOptionalDiscard == true && context.OptionalDiscardAccepted is null)
        {
            return new PendingChoice(
                $"attack:{context.Attack.Name}:{PendingOptionalDiscard}",
                context.ActingPlayerIndex,
                AbilityName: null,
                "Discard an Energy for +30 damage?",
                [new ChoiceOption("yes", "Yes"), new ChoiceOption("no", "No")],
                context.Attack.Name);
        }

        return null;
    }

    private static void DiscardOptionalCost(AttackContext context, AttackBehavior? behavior)
    {
        if (behavior?.NeedsOptionalDiscard != true || context.OptionalDiscardAccepted != true)
            return;
        var board = context.State.Players[context.ActingPlayerIndex];
        var energies = context.EnergiesToDiscard.Count > 0
            ? context.EnergiesToDiscard.ToList()
            : context.Attacker.AttachedEnergies.Take(1).ToList();
        foreach (var energy in energies)
        {
            if (context.Attacker.DetachEnergy(energy))
                board.Discard(energy);
        }
    }

    private static AttackResult Fail(AttackContext context, string reason, int damageDealt = 0)
    {
        context.FailureReason = reason;
        return new AttackResult(false, reason, damageDealt, null);
    }

    public static int ParseBaseDamage(string damage)
    {
        if (string.IsNullOrWhiteSpace(damage))
            return 0;
        var digits = new string(damage.TakeWhile(c => char.IsDigit(c)).ToArray());
        return int.TryParse(digits, out var value) ? value : 0;
    }
}
