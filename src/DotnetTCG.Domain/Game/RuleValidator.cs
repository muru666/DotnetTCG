using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Common;
using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Domain.Game;

public sealed class RuleValidator
{
    public const int RequiredDeckSize = 60;
    public const int MaxCopiesPerName = 4;

    public IReadOnlyList<Error> ValidateDeck(IReadOnlyList<Card> cards)
    {
        var errors = new List<Error>();

        if (cards.Count != RequiredDeckSize)
            errors.Add(new Error("Deck.Size", $"Deck must have exactly {RequiredDeckSize} cards."));

        if (!cards.OfType<PokemonCard>().Any(c => c.IsBasic))
            errors.Add(new Error("Deck.NoBasic", "Deck must contain at least one Basic Pokemon."));

        foreach (var group in cards.GroupBy(c => c.Name))
        {
            if (group.Count() <= MaxCopiesPerName)
                continue;
            if (group.First() is EnergyCard energy && energy.IsBasic)
                continue;
            errors.Add(new Error("Deck.MaxCopies", $"'{group.Key}' exceeds {MaxCopiesPerName} copies."));
        }

        return errors;
    }

    public Error? CanPlayPokemonToBench(GameState state, int playerIndex, PokemonCard card)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(card);

        if (!card.IsBasic)
            return new Error("Play.NotBasic", "Only Basic Pokemon can be played to the bench.");
        if (state.Players[playerIndex].Active is null)
            return new Error("Play.NoActive", "Active Pokemon must be placed first.");
        if (state.Players[playerIndex].Bench.Count >= PlayerBoard.MaxBenchSize)
            return new Error("Play.BenchFull", "Bench is full.");
        return null;
    }

    public Error? CanEvolve(GameState state, PokemonInPlay target, PokemonCard evolution)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(evolution);

        if (state.IsFirstTurn && state.IsStartingPlayerTurn)
            return new Error("Evolve.FirstTurn", "Cannot evolve on the starting player's first turn.");
        if (target.JustPlayed)
            return new Error("Evolve.JustPlayed", "Cannot evolve a Pokemon played this turn.");
        if (target.Card.Subtypes.Contains(SubType.Stage2))
            return new Error("Evolve.MaxStage", "Stage 2 Pokemon cannot evolve further.");
        if (!string.Equals(evolution.EvolvesFrom, target.Card.Name, StringComparison.Ordinal))
            return new Error("Evolve.Mismatch", $"'{evolution.Name}' does not evolve from '{target.Card.Name}'.");
        return null;
    }

    public Error? CanAttachEnergy(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.EnergiesAttachedThisTurn >= 1)
            return new Error("Energy.OncePerTurn", "Only one Energy can be attached per turn.");
        return null;
    }

    public Error? CanPlayTrainer(GameState state, int playerIndex, TrainerCard trainer, PokemonInPlay? toolTarget = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(trainer);

        if (trainer.TrainerSubtype == SubType.Supporter)
        {
            if (state.SupportersPlayedThisTurn >= 1)
                return new Error("Trainer.SupporterOnce", "Only one Supporter per turn.");
            if (state.SupportersBlockedUntilTurn[playerIndex] >= state.TurnNumber)
                return new Error("Trainer.SupporterBlocked", "Supporters are blocked this turn.");
        }

        if (trainer.TrainerSubtype == SubType.Stadium && state.StadiumsPlayedThisTurn >= 1)
            return new Error("Trainer.StadiumOnce", "Only one Stadium per turn.");

        if (trainer.TrainerSubtype == SubType.Item &&
            state.Players[playerIndex].ModifiersBlocked(ModifierType.RestrictItems, state.TurnNumber))
            return new Error("Trainer.ItemsRestricted", "Item cards are restricted this turn.");

        if (trainer.TrainerSubtype == SubType.Tool)
        {
            if (toolTarget is null)
                return new Error("Trainer.ToolNoTarget", "Tool requires a target Pokemon in play.");
            if (toolTarget.AttachedTool is not null)
                return new Error("Trainer.ToolOccupied", "Target already has a tool attached.");
        }

        return null;
    }

    public Error? CanRetreat(GameState state, int playerIndex, IReadOnlyList<EnergyCard> energiesToDiscard)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(energiesToDiscard);

        var board = state.Players[playerIndex];
        var active = board.Active;
        if (active is null)
            return new Error("Retreat.NoActive", "No Active Pokemon to retreat.");
        if (state.RetreatsThisTurn >= 1)
            return new Error("Retreat.OncePerTurn", "Only one retreat per turn.");
        if (active.HasStatus(Status.Asleep) || active.HasStatus(Status.Paralyzed))
            return new Error("Retreat.StatusBlocked", "Active Pokemon cannot retreat while Asleep or Paralyzed.");
        if (active.HasModifier(ModifierType.CannotRetreat, state.TurnNumber)
            || board.ModifiersBlocked(ModifierType.CannotRetreat, state.TurnNumber))
            return new Error("Retreat.Blocked", "Active Pokemon cannot retreat (effect).");
        if (board.Bench.Count == 0)
            return new Error("Retreat.NoBench", "No benched Pokemon to switch in.");

        if (IsFreeRetreat(state, active))
            return null;

        var available = active.TotalEnergyCount;
        var discarded = energiesToDiscard.Sum(e => e.EnergyValue);
        if (available < active.Card.RetreatCost)
            return new Error("Retreat.CostUnpaid", "Not enough Energy to pay the retreat cost.");
        if (discarded < active.Card.RetreatCost)
            return new Error("Retreat.DiscardMissing", "Must discard Energy covering the retreat cost.");
        return null;
    }

    public Error? CanAttack(GameState state, int playerIndex, Attack attack)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(attack);

        var active = state.Players[playerIndex].Active;
        if (active is null)
            return new Error("Attack.NoActive", "No Active Pokemon to attack with.");
        if (state.IsFirstTurn && state.IsStartingPlayerTurn)
            return new Error("Attack.FirstTurn", "The starting player cannot attack on the first turn.");
        if (active.HasStatus(Status.Asleep) || active.HasStatus(Status.Paralyzed))
            return new Error("Attack.StatusBlocked", "Active Pokemon cannot attack while Asleep or Paralyzed.");
        if (active.CannotAttackUntilTurn >= state.TurnNumber)
            return new Error("Attack.Blocked", "Active Pokemon cannot attack (effect).");
        if (active.HasModifier(ModifierType.CannotAttack, state.TurnNumber))
            return new Error("Attack.Blocked", "Active Pokemon cannot attack (effect).");

        var required = attack.Cost.Count;
        if (active.TotalEnergyCount < required)
            return new Error("Attack.EnergyShort", "Active Pokemon lacks the Energy to attack.");
        return null;
    }

    public Error? CanUseAbility(GameState state, PokemonInPlay pokemon, string abilityName)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(pokemon);
        ArgumentException.ThrowIfNullOrWhiteSpace(abilityName);

        if (state.AbilitiesUsedThisTurn >= 1)
            return new Error("Ability.OncePerTurn", "Only one Ability per turn.");
        if (pokemon.HasStatus(Status.Asleep) || pokemon.HasStatus(Status.Paralyzed))
            return new Error("Ability.StatusBlocked", "Pokemon cannot use Abilities while Asleep or Paralyzed.");
        if (!pokemon.Card.Abilities.Any(a => a.Name == abilityName))
            return new Error("Ability.Missing", $"Pokemon has no ability '{abilityName}'.");
        return null;
    }

    private static bool IsFreeRetreat(GameState state, PokemonInPlay active)
    {
        return state.StadiumCard?.Name == Catalogs.StadiumNames.FairyGarden
            && active.Card.PokemonType == PokemonType.Fairy;
    }
}
