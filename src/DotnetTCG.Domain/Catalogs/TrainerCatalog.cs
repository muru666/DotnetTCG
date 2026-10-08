using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Catalogs;

public static class StadiumNames
{
    public const string FairyGarden = "Fairy Garden";
    public const string ShadowCircle = "Shadow Circle";
}

public sealed record TrainerBehavior(
    IReadOnlyList<EffectSpec>? Effects = null,
    bool NeedsTargetChoice = false,
    bool NeedsChoice = false,
    string? ChoiceKind = null,
    BoardModifier? ToolModifier = null);

public static class TrainerCatalog
{
    private static readonly Dictionary<string, TrainerBehavior> Behaviors =
        new(StringComparer.Ordinal)
        {
            ["Potion"] = new TrainerBehavior(
                Effects: [new EffectSpec(EffectType.Heal, Target: EffectTarget.OwnActive, Amount: 30)],
                NeedsTargetChoice: true),
            ["Tierno"] = new TrainerBehavior(
                Effects: [new EffectSpec(EffectType.Draw, Target: EffectTarget.SelfPlayer, Amount: 3)]),
            ["Professor's Letter"] = new TrainerBehavior(
                Effects: [new EffectSpec(EffectType.SearchEnergy, Amount: 2)]),
            ["Cilan"] = new TrainerBehavior(
                Effects: [new EffectSpec(EffectType.SearchEnergy, Amount: 3)]),
            ["Professor Sycamore"] = new TrainerBehavior(
                Effects:
                [
                    new EffectSpec(EffectType.DiscardHand),
                    new EffectSpec(EffectType.Draw, Target: EffectTarget.SelfPlayer, Amount: 7),
                ]),
            ["Team Flare Grunt"] = new TrainerBehavior(
                Effects: [new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.OpponentActive, Amount: 1)]),
            ["Shauna"] = new TrainerBehavior(
                Effects:
                [
                    new EffectSpec(EffectType.ShuffleHand, Target: EffectTarget.SelfPlayer),
                    new EffectSpec(EffectType.Draw, Target: EffectTarget.SelfPlayer, Amount: 5),
                ]),
            ["Super Potion"] = new TrainerBehavior(
                Effects:
                [
                    new EffectSpec(EffectType.Heal, Target: EffectTarget.OwnActive, Amount: 60),
                    new EffectSpec(EffectType.DiscardEnergy, Target: EffectTarget.SelfPlayer, Amount: 1),
                ],
                NeedsTargetChoice: true),
            ["Roller Skates"] = new TrainerBehavior(
                Effects: [new EffectSpec(EffectType.Draw, Target: EffectTarget.SelfPlayer, Amount: 3, FlipToActivate: true)]),
            ["Red Card"] = new TrainerBehavior(
                Effects:
                [
                    new EffectSpec(EffectType.ShuffleHand, Target: EffectTarget.OpponentPlayer),
                    new EffectSpec(EffectType.Draw, Target: EffectTarget.OpponentPlayer, Amount: 4),
                ]),
            ["Muscle Band"] = new TrainerBehavior(ToolModifier: new BoardModifier(
                ModifierType.DamageBoost, "Muscle Band", 20, ModifierDuration.Permanent, 0)),
            ["Hard Charm"] = new TrainerBehavior(ToolModifier: new BoardModifier(
                ModifierType.DamageReduction, "Hard Charm", 20, ModifierDuration.Permanent, 0)),
            ["Evosoda"] = new TrainerBehavior(NeedsChoice: true, ChoiceKind: "evosoda"),
            ["Great Ball"] = new TrainerBehavior(NeedsChoice: true, ChoiceKind: "great-ball"),
            ["Cassius"] = new TrainerBehavior(NeedsChoice: true, ChoiceKind: "cassius"),
            ["Max Revive"] = new TrainerBehavior(NeedsChoice: true, ChoiceKind: "max-revive"),
        };

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["Letter"] = "Professor's Letter",
        ["Sycamore"] = "Professor Sycamore",
        ["Grunt"] = "Team Flare Grunt",
    };

    public static TrainerBehavior? Find(string trainerName)
    {
        if (Behaviors.TryGetValue(trainerName, out var behavior))
            return behavior;
        return Aliases.TryGetValue(trainerName, out var canonical)
            && Behaviors.TryGetValue(canonical, out var aliased)
            ? aliased
            : null;
    }
}
