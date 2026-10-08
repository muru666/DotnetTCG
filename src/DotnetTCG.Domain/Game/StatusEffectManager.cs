using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Domain.Game;

public sealed class StatusEffectManager
{
    public const int PoisonDamage = 10;
    public const int BurnDamage = 20;
    public const int ConfusionSelfDamage = 30;

    private static readonly HashSet<Status> ExclusiveStatuses =
        [Status.Asleep, Status.Confused, Status.Paralyzed];

    public bool TryApplyStatus(PokemonInPlay pokemon, Status status)
    {
        ArgumentNullException.ThrowIfNull(pokemon);

        if (HasSweetVeilImmunity(pokemon))
            return false;

        if (ExclusiveStatuses.Contains(status))
        {
            foreach (var exclusive in ExclusiveStatuses)
                pokemon.RemoveStatus(exclusive);
        }

        pokemon.AddStatus(status);
        return true;
    }

    public void ProcessBetweenTurns(PokemonInPlay pokemon, ICoinFlipService coinFlip)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        ArgumentNullException.ThrowIfNull(coinFlip);

        if (pokemon.HasStatus(Status.Poisoned))
            pokemon.AddDamage(PoisonDamage);

        if (pokemon.HasStatus(Status.Burned) && !coinFlip.FlipHeads())
            pokemon.AddDamage(BurnDamage);

        if (pokemon.HasStatus(Status.Asleep) && coinFlip.FlipHeads())
            pokemon.RemoveStatus(Status.Asleep);

        if (pokemon.HasStatus(Status.Paralyzed))
            pokemon.RemoveStatus(Status.Paralyzed);
    }

    public void CureOnBenchedOrEvolved(PokemonInPlay pokemon)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        pokemon.ClearAllStatuses();
    }

    private static bool HasSweetVeilImmunity(PokemonInPlay pokemon)
        => pokemon.AttachedEnergies.Any(e => e.EnergyType == PokemonType.Fairy);
}
