using DotnetTCG.Domain.Attacks;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Engine.Handlers;

public sealed class AttackHandler(EngineServices services)
{
    public string? Handle(GameState state, GameAction action, List<GameEvent> events)
    {
        if (action.Attack is null)
            return "Attack.CardRequired";

        var active = state.Players[action.PlayerIndex].Active;
        if (active is null)
            return "Attack.NoActive";

        var attack = active.Card.Attacks.FirstOrDefault(a => a.Name == action.Attack.Name);
        if (attack is null)
            return "Attack.Unknown";

        var error = services.Rules.CanAttack(state, action.PlayerIndex, attack);
        if (error is not null)
            return error.Code;

        bool? discardAccepted = null;
        if (action.SelectedOptionIds?.Contains("yes") == true)
            discardAccepted = true;
        else if (action.SelectedOptionIds?.Contains("no") == true)
            discardAccepted = false;
        else if ((action.EnergiesToDiscard?.Count ?? 0) > 0)
            discardAccepted = true;

        var context = new AttackContext
        {
            State = state,
            ActingPlayerIndex = action.PlayerIndex,
            Attack = attack,
            Attacker = active,
            Defender = state.OpponentOf(action.PlayerIndex).Active
                ?? throw new InvalidOperationException("Opponent has no Active Pokemon."),
            OptionalDiscardAccepted = discardAccepted,
        };
        if (action.EnergiesToDiscard is not null)
            context.EnergiesToDiscard.AddRange(action.EnergiesToDiscard);

        var behavior = services.Attacks.Find(attack.Name);
        var result = services.Pipeline.Execute(context, behavior);

        if (result.Pending is not null)
        {
            state.SetPendingAttack(new PendingAttack(attack.Name, action.PlayerIndex));
            state.SetPendingChoice(result.Pending);
            return null;
        }

        if (!result.Success)
            return result.FailureReason;

        events.Add(GameEvent.Attack);
        return null;
    }

    public string? Resume(GameState state, GameAction action, List<GameEvent> events)
    {
        var pendingAttack = state.PendingAttack;
        var pending = state.PendingChoice;
        if (pendingAttack is null || pending is null || action.ChoiceId != pending.ChoiceId)
            return "Choice.Unknown";

        var active = state.Players[pendingAttack.ActingPlayerIndex].Active;
        if (active is null)
            return "Attack.NoActive";

        var attack = active.Card.Attacks.FirstOrDefault(a => a.Name == pendingAttack.AttackName);
        if (attack is null)
            return "Attack.Unknown";

        var context = new AttackContext
        {
            State = state,
            ActingPlayerIndex = pendingAttack.ActingPlayerIndex,
            Attack = attack,
            Attacker = active,
            Defender = state.OpponentOf(pendingAttack.ActingPlayerIndex).Active
                ?? throw new InvalidOperationException("Opponent has no Active Pokemon."),
        };

        foreach (var optionId in action.SelectedOptionIds ?? [])
        {
            if (pending.ChoiceId.Contains($":{AttackPipeline.PendingBenchTarget}:")
                && int.TryParse(optionId, out var benchIndex))
            {
                var ownSide = pending.ChoiceId.EndsWith(":own", StringComparison.Ordinal);
                var bench = ownSide
                    ? state.Players[pendingAttack.ActingPlayerIndex].Bench.ToList()
                    : OpponentBench(state, pendingAttack.ActingPlayerIndex);
                if (benchIndex < 0 || benchIndex >= bench.Count)
                    return "Choice.InvalidOption";
                context.SelectedTarget = bench[benchIndex];
            }
            else if (pending.ChoiceId.EndsWith($":{AttackPipeline.PendingOptionalDiscard}", StringComparison.Ordinal))
            {
                context.OptionalDiscardAccepted = optionId == "yes";
            }
        }

        if (action.EnergiesToDiscard is not null)
            context.EnergiesToDiscard.AddRange(action.EnergiesToDiscard);

        var behavior = services.Attacks.Find(attack.Name);
        var result = services.Pipeline.Resume(context, behavior);

        if (result.Pending is not null)
        {
            state.SetPendingAttack(pendingAttack);
            state.SetPendingChoice(result.Pending);
            return null;
        }

        state.SetPendingChoice(null);

        if (!result.Success)
        {
            state.SetPendingAttack(null);
            return result.FailureReason;
        }

        events.Add(GameEvent.Attack);
        return null;
    }

    private static List<PokemonInPlay> OpponentBench(GameState state, int actingPlayer)
        => state.OpponentOf(actingPlayer).Bench.ToList();
}

public sealed class AbilityHandler(EngineServices services)
{
    public string? Handle(GameState state, GameAction action, List<GameEvent> events)
    {
        if (action.Target is null || action.AbilityName is null)
            return "Ability.TargetRequired";

        // La habilidad la usa un Pokémon propio (FindAbilityUser sólo busca en
        // el campo propio): el usuario que manda el cliente debe validarse.
        var own = state.Players[action.PlayerIndex];
        if (own.Active != action.Target && !own.Bench.Contains(action.Target))
            return "Ability.ForeignTarget";

        var error = services.Rules.CanUseAbility(state, action.Target, action.AbilityName);
        if (error is not null)
            return error.Code;

        var behavior = services.Abilities.Find(action.AbilityName);
        if (behavior is null)
            return "Ability.Unknown";

        if (behavior.NeedsTargetChoice)
        {
            var hasBench = state.OpponentOf(action.PlayerIndex).Bench.Count > 0;
            if (action.SelectedOptionIds is null && hasBench)
            {
                var opponent = state.OpponentOf(action.PlayerIndex);
                var options = new List<ChoiceOption> { new("active", opponent.Active?.Card.Name ?? "Active") };
                options.AddRange(opponent.Bench.Select((p, i) => new ChoiceOption($"bench:{i}", p.Card.Name)));
                state.SetPendingChoice(new PendingChoice(
                    $"ability:{action.AbilityName}:target",
                    action.PlayerIndex,
                    action.AbilityName,
                    $"Choose a target for {action.AbilityName}.",
                    options,
                    action.AbilityName));
                return null;
            }

            PokemonInPlay? selected;
            if (action.SelectedOptionIds is null)
            {
                selected = state.OpponentOf(action.PlayerIndex).Active;
            }
            else
            {
                selected = ResolveTarget(state, action.PlayerIndex, action.SelectedOptionIds.FirstOrDefault());
                if (selected is null)
                    return "Choice.InvalidOption";
            }

            ApplyBehavior(state, action, behavior, user: action.Target, selected);
            state.RegisterAbilityUsed();
            return null;
        }

        ApplyBehavior(state, action, behavior, user: action.Target, selected: null);
        state.RegisterAbilityUsed();
        return null;
    }

    public string? Resume(GameState state, GameAction action, List<GameEvent> events)
    {
        var pending = state.PendingChoice;
        if (pending is null || action.ChoiceId != pending.ChoiceId || pending.AbilityName is null)
            return "Choice.Unknown";

        var behavior = services.Abilities.Find(pending.AbilityName);
        if (behavior is null)
            return "Ability.Unknown";

        var user = FindAbilityUser(state, action.PlayerIndex, pending.AbilityName);
        if (user is null)
            return "Ability.TargetRequired";

        var selected = ResolveTarget(state, action.PlayerIndex, action.SelectedOptionIds?.FirstOrDefault());
        ApplyBehavior(state, action, behavior, user, selected);
        state.RegisterAbilityUsed();
        state.SetPendingChoice(null);
        return null;
    }

    private void ApplyBehavior(
        GameState state, GameAction action, Abilities.AbilityBehavior behavior, PokemonInPlay user, PokemonInPlay? selected)
    {
        if (behavior.Effects is not null)
        {
            var context = new EffectContext(state, action.PlayerIndex, user, selected);
            foreach (var spec in behavior.Effects)
                services.Effects.Apply(context, spec);
        }

        behavior.Custom?.Invoke(new Abilities.AbilityContext(state, action.PlayerIndex, user, selected), services.Effects);
    }

    private static PokemonInPlay? FindAbilityUser(GameState state, int playerIndex, string abilityName)
    {
        var board = state.Players[playerIndex];
        var field = new List<PokemonInPlay>();
        if (board.Active is not null)
            field.Add(board.Active);
        field.AddRange(board.Bench);
        return field.FirstOrDefault(p => p.Card.Abilities.Any(a => a.Name == abilityName));
    }

    private static PokemonInPlay? ResolveTarget(GameState state, int playerIndex, string? optionId)
    {
        var opponent = state.OpponentOf(playerIndex);
        if (optionId is null || optionId == "active")
            return opponent.Active;
        if (optionId.StartsWith("bench:", StringComparison.Ordinal)
            && int.TryParse(optionId["bench:".Length..], out var index)
            && index >= 0 && index < opponent.Bench.Count)
            return opponent.Bench[index];
        return null;
    }
}
