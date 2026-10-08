using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Domain.Game;

public sealed class PokemonInPlay
{
    private readonly List<EnergyCard> _attachedEnergies = [];
    private readonly HashSet<Status> _statuses = [];
    private readonly List<BoardModifier> _modifiers = [];

    public PokemonInPlay(PokemonCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        Card = card;
    }

    public PokemonCard Card { get; private set; }
    public int TotalDamage { get; private set; }
    public TrainerCard? AttachedTool { get; private set; }
    public bool JustPlayed { get; set; } = true;
    public int CannotAttackUntilTurn { get; set; }
    public IReadOnlyList<EnergyCard> AttachedEnergies => _attachedEnergies.AsReadOnly();
    public IReadOnlySet<Status> Statuses => _statuses;
    public IReadOnlyList<BoardModifier> Modifiers => _modifiers.AsReadOnly();

    public int DamageCounters => TotalDamage / 10;
    public int HpRemaining => Math.Max(0, Card.Hp - TotalDamage);
    public bool KnockedOut => TotalDamage >= Card.Hp;

    public int TotalEnergyCount => _attachedEnergies.Sum(e => e.EnergyValue);

    public void AddDamage(int damage)
    {
        if (damage < 0)
            throw new ArgumentOutOfRangeException(nameof(damage));
        TotalDamage += damage;
    }

    public void Heal(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        TotalDamage = Math.Max(0, TotalDamage - amount);
    }

    public void HealAll() => TotalDamage = 0;

    public void AttachEnergy(EnergyCard energy)
    {
        ArgumentNullException.ThrowIfNull(energy);
        _attachedEnergies.Add(energy);
    }

    public IReadOnlyList<EnergyCard> DiscardEnergies(int count)
    {
        var discarded = _attachedEnergies.Take(count).ToList();
        foreach (var energy in discarded)
            _attachedEnergies.Remove(energy);
        return discarded;
    }

    public void AttachTool(TrainerCard tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (AttachedTool is not null)
            throw new InvalidOperationException("Pokemon already has a tool attached.");
        AttachedTool = tool;
    }

    public void AddStatus(Status status) => _statuses.Add(status);
    public bool HasStatus(Status status) => _statuses.Contains(status);
    public void RemoveStatus(Status status) => _statuses.Remove(status);
    public void ClearAllStatuses() => _statuses.Clear();

    public void AddModifier(BoardModifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        _modifiers.Add(modifier);
    }

    public void RemoveExpiredModifiers(int currentTurn)
    {
        _modifiers.RemoveAll(m =>
            m.Duration == ModifierDuration.OneUse ||
            (m.Duration == ModifierDuration.UntilNextTurn && currentTurn > m.TurnApplied));
    }

    public void EvolveInto(PokemonCard evolution)
    {
        ArgumentNullException.ThrowIfNull(evolution);
        Card = evolution;
        ClearAllStatuses();
        JustPlayed = true;
    }
}
