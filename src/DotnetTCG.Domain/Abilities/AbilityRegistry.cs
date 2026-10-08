using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Abilities;

public sealed record AbilityContext(
    GameState State,
    int ActingPlayerIndex,
    PokemonInPlay User,
    PokemonInPlay? SelectedTarget);

public sealed record AbilityBehavior(
    IReadOnlyList<EffectSpec>? Effects = null,
    Action<AbilityContext, EffectEngine>? Custom = null,
    bool NeedsTargetChoice = false);

public sealed class AbilityRegistry
{
    private readonly Dictionary<string, AbilityBehavior> _behaviors;

    public AbilityRegistry()
    {
        _behaviors = new Dictionary<string, AbilityBehavior>(StringComparer.Ordinal)
        {
            ["Mystical Fire"] = new AbilityBehavior(
                Effects: [new EffectSpec(EffectType.DrawUntil, Target: EffectTarget.SelfPlayer, Amount: 6)]),
            ["Water Shuriken"] = new AbilityBehavior(
                NeedsTargetChoice: true,
                Custom: static (ctx, effects) =>
                {
                    var board = ctx.State.Players[ctx.ActingPlayerIndex];
                    var water = board.Hand.OfType<EnergyCard>()
                        .FirstOrDefault(e => e.EnergyType == PokemonType.Water);
                    if (water is null)
                        return;
                    board.RemoveFromHand(water);
                    board.Discard(water);
                    var target = ctx.SelectedTarget
                        ?? ctx.State.OpponentOf(ctx.ActingPlayerIndex).Active;
                    target?.AddDamage(30);
                }),
            ["Upside-Down"] = new AbilityBehavior(
                Custom: static (ctx, _) =>
                {
                    if (!ctx.User.HasStatus(Status.Confused))
                        return;
                    var board = ctx.State.Players[ctx.ActingPlayerIndex];
                    var evolution = board.Hand.OfType<PokemonCard>()
                        .FirstOrDefault(c => c.EvolvesFrom == ctx.User.Card.Name);
                    if (evolution is null)
                        return;
                    board.RemoveFromHand(evolution);
                    ctx.User.EvolveInto(evolution);
                }),
            ["Stance Change"] = new AbilityBehavior(
                Custom: static (ctx, _) =>
                {
                    var board = ctx.State.Players[ctx.ActingPlayerIndex];
                    if (board.Active != ctx.User && board.Bench.Contains(ctx.User))
                    {
                        board.SwitchActive(ctx.User);
                        return;
                    }

                    var replacement = board.Bench.FirstOrDefault();
                    if (board.Active == ctx.User && replacement is not null)
                        board.SwitchActive(replacement);
                }),
            ["Fairy Transfer"] = new AbilityBehavior(
                Effects: [new EffectSpec(EffectType.MoveEnergy, EnergyType: PokemonType.Fairy)]),
            ["Drive Off"] = new AbilityBehavior(
                Effects: [new EffectSpec(EffectType.OpponentSwitch)]),
        };
    }

    public AbilityBehavior? Find(string abilityName)
        => _behaviors.TryGetValue(abilityName, out var behavior) ? behavior : null;
}
