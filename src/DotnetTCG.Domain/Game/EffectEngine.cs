using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Domain.Game;

public sealed class EffectEngine(
    StatusEffectManager statuses,
    ICoinFlipService coinFlip,
    Random random)
{
    public const int PreventAllValue = 9999;

    public void Apply(EffectContext context, EffectSpec spec)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(spec);

        if (spec.FlipToActivate && !coinFlip.FlipHeads())
            return;
        if (spec.ApplyOnTailsOnly && coinFlip.FlipHeads())
            return;

        switch (spec.Type)
        {
            case EffectType.ApplyStatus:
                ApplyToTargets(context, spec, t => statuses.TryApplyStatus(t, spec.Status!.Value));
                break;
            case EffectType.Heal:
                ApplyToTargets(context, spec, t => t.Heal(spec.Amount));
                break;
            case EffectType.SelfDamage:
                context.Source?.AddDamage(spec.Amount);
                break;
            case EffectType.Draw:
                DrawCards(DrawTargetBoard(context, spec), spec.Amount);
                break;
            case EffectType.DiscardEnergy:
                ApplyToTargets(context, spec, t => DiscardAttached(context, t, spec));
                break;
            case EffectType.ShuffleHand:
                TargetPlayerBoard(context, spec).ShuffleHandIntoDeck(random);
                break;
            case EffectType.DiscardOpponentHand:
                DiscardOpponentHand(context, spec.Amount);
                break;
            case EffectType.DiscardHand:
                context.State.Players[context.ActingPlayerIndex].DiscardHand();
                break;
            case EffectType.SearchEnergy:
                SearchMultiple(context, c => IsEnergyMatch(c, spec.EnergyType),
                    spec.Amount <= 0 ? 1 : spec.Amount);
                break;
            case EffectType.HealAll:
                foreach (var pokemon in OwnField(context))
                    pokemon.HealAll();
                break;
            case EffectType.SwitchActive:
                SwitchOwnActive(context);
                break;
            case EffectType.SearchPokemon:
                SearchMultiple(context, c => c is PokemonCard, spec.Amount <= 0 ? 1 : spec.Amount);
                break;
            case EffectType.Revive:
                Revive(context);
                break;
            case EffectType.DiscardDeck:
                context.State.Players[context.ActingPlayerIndex].MillTop(spec.Amount);
                break;
            case EffectType.PutCounters:
                ApplyToTargets(context, spec, t => t.AddDamage(spec.Amount * 10));
                break;
            case EffectType.DrawUntil:
                DrawUntil(context, spec);
                break;
            case EffectType.MoveEnergy:
                MoveEnergy(context, spec);
                break;
            case EffectType.OpponentSwitch:
                ForceOpponentSwitch(context);
                break;
            case EffectType.DiscardAll:
                ApplyToTargets(context, spec, t => DiscardAttached(context, t, spec with { Amount = int.MaxValue }));
                break;
            case EffectType.RemoveStatus:
                ApplyToTargets(context, spec, t => t.ClearAllStatuses());
                break;
            case EffectType.DrawShuffle:
                DrawShuffled(context);
                break;
            case EffectType.DiscardOpponentDeck:
                context.State.OpponentOf(context.ActingPlayerIndex).MillTop(spec.Amount);
                break;
            case EffectType.HealBench:
                foreach (var benched in context.State.Players[context.ActingPlayerIndex].Bench)
                    benched.Heal(spec.Amount);
                break;
            case EffectType.PreventRetreat:
                AddPlayerModifier(context, ModifierType.CannotRetreat, spec);
                break;
            case EffectType.RestrictAttack:
                AddPokemonModifier(context, spec, ModifierType.CannotAttack);
                break;
            case EffectType.RestrictItems:
                AddPlayerModifier(context, ModifierType.RestrictItems, spec);
                break;
            case EffectType.FlipStatus:
                if (coinFlip.FlipHeads())
                    ApplyToTargets(context, spec, t => statuses.TryApplyStatus(t, spec.Status!.Value));
                break;
            case EffectType.FlipEnergy:
                if (coinFlip.FlipHeads())
                    AttachSearchedEnergy(context, spec);
                break;
            case EffectType.Prevent:
                AddPokemonModifier(context, spec,
                    ModifierType.PreventDamage, PreventAllValue, ModifierDuration.UntilNextTurn);
                break;
            case EffectType.SearchAny:
                SearchAndTake(context, c =>
                    (spec.CardName == null || c.Name == spec.CardName) &&
                    (spec.TrainerSubtype == null || c.Subtypes.Contains(spec.TrainerSubtype.Value)));
                break;
            case EffectType.Recycle:
                Recycle(context, spec.Amount <= 0 ? 1 : spec.Amount);
                break;
            case EffectType.FlipCoin:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(spec));
        }
    }

    private static bool IsEnergyMatch(Card card, PokemonType? type)
        => card is EnergyCard energy && (type == null || energy.EnergyType == type);

    private void ApplyToTargets(EffectContext context, EffectSpec spec, Action<PokemonInPlay> action)
    {
        foreach (var target in ResolveTargets(context, spec))
            action(target);
    }

    private IReadOnlyList<PokemonInPlay> ResolveTargets(EffectContext context, EffectSpec spec)
    {
        var own = context.State.Players[context.ActingPlayerIndex];
        var opponent = context.State.OpponentOf(context.ActingPlayerIndex);
        var targets = new List<PokemonInPlay>();

        switch (spec.Target)
        {
            case EffectTarget.OwnActive:
                // Si el llamador eligió objetivo (TrainerHandler manda Source ==
                // SelectedTarget), la cura va al elegido: con Super Potion/Potion
                // la cura se iba siempre al activo mientras la energía se
                // descartaba del elegido (efecto partido).
                var ownActive = context.Source is not null
                    && context.Source == context.SelectedTarget
                    && context.SelectedTarget is not null
                        ? context.SelectedTarget
                        : own.Active;
                if (ownActive is not null)
                    targets.Add(ownActive);
                break;
            case EffectTarget.OwnBench:
                if (context.SelectedTarget is not null)
                    targets.Add(context.SelectedTarget);
                else
                    targets.AddRange(own.Bench);
                break;
            case EffectTarget.AllOwn:
                targets.AddRange(OwnField(context));
                break;
            case EffectTarget.OpponentActive:
                if (opponent.Active is not null)
                    targets.Add(opponent.Active);
                break;
            case EffectTarget.OpponentBench:
                if (context.SelectedTarget is not null)
                    targets.Add(context.SelectedTarget);
                else
                    targets.AddRange(opponent.Bench);
                break;
            case EffectTarget.AllOpponents:
                targets.AddRange(OpponentField(context));
                break;
            case EffectTarget.SelfPlayer:
            case EffectTarget.OpponentPlayer:
                if (context.Source is not null)
                    targets.Add(context.Source);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(spec));
        }

        return targets;
    }

    private List<PokemonInPlay> OwnField(EffectContext context)
    {
        var own = context.State.Players[context.ActingPlayerIndex];
        var field = new List<PokemonInPlay>();
        if (own.Active is not null)
            field.Add(own.Active);
        field.AddRange(own.Bench);
        return field;
    }

    private List<PokemonInPlay> OpponentField(EffectContext context)
    {
        var opponent = context.State.OpponentOf(context.ActingPlayerIndex);
        var field = new List<PokemonInPlay>();
        if (opponent.Active is not null)
            field.Add(opponent.Active);
        field.AddRange(opponent.Bench);
        return field;
    }

    private static void DrawCards(PlayerBoard board, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (board.DrawCard().IsFailure)
                break;
        }
    }

    private void DiscardOpponentHand(EffectContext context, int leaveCount)
    {
        var opponent = context.State.OpponentOf(context.ActingPlayerIndex);
        while (opponent.Hand.Count > leaveCount)
        {
            var card = opponent.Hand[^1];
            opponent.RemoveFromHand(card);
            opponent.Discard(card);
        }
    }

    private void DrawUntil(EffectContext context, EffectSpec spec)
    {
        var board = DrawTargetBoard(context, spec);
        while (board.Hand.Count < spec.Amount)
        {
            if (board.DrawCard().IsFailure)
                break;
        }
    }

    private static PlayerBoard DrawTargetBoard(EffectContext context, EffectSpec? spec)
        => spec?.Target == EffectTarget.OpponentPlayer
            ? context.State.OpponentOf(context.ActingPlayerIndex)
            : context.State.Players[context.ActingPlayerIndex];

    private void SearchAndTake(EffectContext context, Func<Card, bool> predicate)
    {
        SearchMultiple(context, predicate, 1);
    }

    private void SearchMultiple(EffectContext context, Func<Card, bool> predicate, int count)
    {
        var board = context.State.Players[context.ActingPlayerIndex];
        for (var i = 0; i < count; i++)
        {
            var found = board.RemoveFromDeck(predicate);
            if (found is null)
                break;
            board.AddToHand(found);
        }

        board.ShuffleDeck(random);
    }

    private void Revive(EffectContext context)
    {
        var board = context.State.Players[context.ActingPlayerIndex];
        var recycled = board.RecycleFirst();
        if (recycled is PokemonCard pokemon && board.Bench.Count < PlayerBoard.MaxBenchSize)
            board.AddToBench(new PokemonInPlay(pokemon));
    }

    private void SwitchOwnActive(EffectContext context)
    {
        var board = context.State.Players[context.ActingPlayerIndex];
        var replacement = context.SelectedTarget ?? board.Bench.FirstOrDefault();
        if (board.Active is null || replacement is null)
            return;
        board.SwitchActive(replacement);
    }

    private void ForceOpponentSwitch(EffectContext context)
    {
        var opponent = context.State.OpponentOf(context.ActingPlayerIndex);
        var replacement = opponent.Bench.FirstOrDefault();
        if (opponent.Active is null || replacement is null)
            return;
        opponent.SwitchActive(replacement);
    }

    private static void DiscardAttached(EffectContext context, PokemonInPlay target, EffectSpec spec)
    {
        var energies = target.AttachedEnergies
            .Where(e => spec.EnergyType == null || e.EnergyType == spec.EnergyType)
            .Take(spec.Amount)
            .ToList();
        if (energies.Count == 0)
            return;

        var owner = OwnerBoardOf(context, target);
        foreach (var energy in energies)
        {
            target.DetachEnergy(energy);
            owner.Discard(energy);
        }
    }

    private static PlayerBoard OwnerBoardOf(EffectContext context, PokemonInPlay target)
    {
        var own = context.State.Players[context.ActingPlayerIndex];
        if (own.Active == target || own.Bench.Contains(target))
            return own;
        return context.State.OpponentOf(context.ActingPlayerIndex);
    }

    private static void DiscardEnergies(PokemonInPlay target, EffectSpec spec)
    {
        var energies = target.AttachedEnergies
            .Where(e => spec.EnergyType == null || e.EnergyType == spec.EnergyType)
            .Take(spec.Amount)
            .ToList();
        target.DiscardEnergies(energies.Count);
    }

    private void MoveEnergy(EffectContext context, EffectSpec spec)
    {
        if (context.Source is null)
            return;
        var energy = context.Source.AttachedEnergies
            .FirstOrDefault(e => spec.EnergyType == null || e.EnergyType == spec.EnergyType);
        if (energy is null)
            return;

        var destination = context.SelectedTarget
            ?? context.State.Players[context.ActingPlayerIndex].Bench.FirstOrDefault();
        if (destination is null || destination == context.Source)
            return;

        if (!context.Source.DetachEnergy(energy))
            return;
        destination.AttachEnergy(energy);
    }

    private void AttachSearchedEnergy(EffectContext context, EffectSpec spec)
    {
        if (context.Source is null)
            return;
        var board = context.State.Players[context.ActingPlayerIndex];
        var found = board.RemoveFromDeck(c => IsEnergyMatch(c, spec.EnergyType));
        if (found is EnergyCard energy)
            context.Source.AttachEnergy(energy);
        board.ShuffleDeck(random);
    }

    private void DrawShuffled(EffectContext context)
    {
        var board = context.State.Players[context.ActingPlayerIndex];
        var count = board.Hand.Count;
        board.ShuffleHandIntoDeck(random);
        DrawCards(board, count);
    }

    private void AddPlayerModifier(EffectContext context, ModifierType type, EffectSpec spec)
    {
        TargetPlayerBoard(context, spec).AddModifier(new BoardModifier(
            type, "Effect", 0, ModifierDuration.UntilNextTurn, context.State.TurnNumber));
    }

    private static PlayerBoard TargetPlayerBoard(EffectContext context, EffectSpec spec)
        => spec.Target is EffectTarget.OpponentPlayer or EffectTarget.AllOpponents
            or EffectTarget.OpponentActive or EffectTarget.OpponentBench
            ? context.State.OpponentOf(context.ActingPlayerIndex)
            : context.State.Players[context.ActingPlayerIndex];

    private static void Recycle(EffectContext context, int count)
    {
        var board = context.State.Players[context.ActingPlayerIndex];
        for (var i = 0; i < count; i++)
        {
            if (board.RecycleFirst() is null)
                break;
        }
    }

    public void AttachFromDeck(EffectContext context, PokemonType? energyType, PokemonInPlay destination)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(destination);
        var board = context.State.Players[context.ActingPlayerIndex];
        var found = board.RemoveFromDeck(c => IsEnergyMatch(c, energyType));
        if (found is EnergyCard energy)
            destination.AttachEnergy(energy);
        board.ShuffleDeck(random);
    }

    private void AddPokemonModifier(
        EffectContext context,
        EffectSpec spec,
        ModifierType type,
        int value = 0,
        ModifierDuration duration = ModifierDuration.UntilNextTurn)
    {
        foreach (var target in ResolveTargets(context, spec))
            target.AddModifier(new BoardModifier(type, "Effect", value, duration, context.State.TurnNumber));
    }
}
