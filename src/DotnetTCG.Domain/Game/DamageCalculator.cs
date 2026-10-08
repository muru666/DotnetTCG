using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Game;

public sealed record DamageCalculationInput(
    int BaseDamage,
    string AttackName,
    PokemonInPlay Attacker,
    PokemonInPlay Defender,
    Cards.TrainerCard? StadiumCard,
    bool IgnoreWeakness = false,
    bool IgnoreResistance = false);

public sealed class DamageCalculator
{
    public const int DefaultWeaknessMultiplier = 2;
    public const int DefaultResistanceReduction = 20;
    public const int FurCoatReduction = 20;

    public static readonly IReadOnlySet<string> AttacksIgnoringWeakness =
        new HashSet<string>(StringComparer.Ordinal) { "Mist Slash" };

    public static readonly IReadOnlySet<string> AttacksIgnoringResistance =
        new HashSet<string>(StringComparer.Ordinal);

    public int Calculate(DamageCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var damage = input.BaseDamage;

        damage += input.Attacker.Modifiers
            .Where(m => m.Type == ModifierType.DamageBoost)
            .Sum(m => m.Value);

        damage = ApplyWeakness(input, damage);
        damage = ApplyResistance(input, damage);

        damage -= input.Defender.Modifiers
            .Where(m => m.Type == ModifierType.DamageReduction)
            .Sum(m => m.Value);

        if (input.Defender.Modifiers.Any(m => m.Type == ModifierType.PreventDamage))
            return 0;

        if (input.Defender.Card.Abilities.Any(a => a.Name == "Fur Coat"))
            damage -= FurCoatReduction;

        return Math.Max(0, damage);
    }

    private static int ApplyWeakness(DamageCalculationInput input, int damage)
    {
        if (input.IgnoreWeakness || AttacksIgnoringWeakness.Contains(input.AttackName))
            return damage;
        if (IsShadowCircleActive(input))
            return damage;

        var attackerType = input.Attacker.Card.PokemonType;
        var weakness = input.Defender.Card.Weaknesses.FirstOrDefault(w => w.Type == attackerType);
        if (weakness is null)
            return damage;

        return damage * ParseMultiplier(weakness.Multiplier);
    }

    private static int ApplyResistance(DamageCalculationInput input, int damage)
    {
        if (input.IgnoreResistance || AttacksIgnoringResistance.Contains(input.AttackName))
            return damage;

        var attackerType = input.Attacker.Card.PokemonType;
        var resistance = input.Defender.Card.Resistances.FirstOrDefault(r => r.Type == attackerType);
        if (resistance is null)
            return damage;

        return damage - Math.Abs(ParseReduction(resistance.Reduction));
    }

    private static bool IsShadowCircleActive(DamageCalculationInput input)
        => input.StadiumCard?.Name == "Shadow Circle"
            && input.Attacker.Card.PokemonType == PokemonType.Darkness;

    private static int ParseMultiplier(string multiplier)
    {
        var digits = new string(multiplier.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) && value > 0 ? value : DefaultWeaknessMultiplier;
    }

    private static int ParseReduction(string reduction)
    {
        var digits = new string(reduction.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) && value > 0 ? value : DefaultResistanceReduction;
    }
}
