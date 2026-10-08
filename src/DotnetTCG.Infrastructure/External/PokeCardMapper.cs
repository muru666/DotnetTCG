using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Infrastructure.External;

internal static class PokeCardMapper
{
    public static Card ToDomain(PokeCardDto dto)
    {
        var supertype = dto.Supertype switch
        {
            "Pokémon" => SuperType.Pokemon,
            "Energy" => SuperType.Energy,
            "Trainer" => SuperType.Trainer,
            _ => throw new InvalidOperationException($"Unknown supertype '{dto.Supertype}' ({dto.Id})."),
        };
        var rules = dto.Rules ?? [];

        return supertype switch
        {
            SuperType.Pokemon => new PokemonCard(
                dto.Id, dto.Name, dto.Subtypes.Select(MapSubType).ToList(), rules, dto.Set.Id, dto.Images.Small,
                ParseHp(dto.Hp), dto.RetreatCost?.Count ?? 0, dto.EvolvesFrom,
                MapType(dto.Types?.FirstOrDefault() ?? "Colorless"),
                (dto.Attacks ?? []).Select(a => new Attack(
                    a.Name, a.Text, a.Damage ?? string.Empty,
                    a.Cost.Select(MapType).ToList())).ToList(),
                (dto.Abilities ?? []).Select(a => new PokemonAbility(
                    a.Name, a.Text, MapAbilityType(a.Type))).ToList(),
                (dto.Weaknesses ?? []).Select(w => new Weakness(MapType(w.Type), w.Value)).ToList(),
                (dto.Resistances ?? []).Select(r => new Resistance(MapType(r.Type), r.Value)).ToList()),
            SuperType.Energy => MapEnergy(dto, rules),
            SuperType.Trainer => new TrainerCard(
                dto.Id, dto.Name, dto.Subtypes.Select(MapSubType).ToList(), rules, dto.Set.Id, dto.Images.Small,
                MapTrainerSubtype(dto.Subtypes)),
            _ => throw new InvalidOperationException($"Unhandled supertype ({dto.Id})."),
        };
    }

    private static EnergyCard MapEnergy(PokeCardDto dto, List<string> rules)
    {
        // Las energías no usan SubType: lo básico/especial vive en Special (null = básica).
        var subtypes = new List<SubType>();
        if (dto.Name.Contains("Double Colorless", StringComparison.OrdinalIgnoreCase))
            return new EnergyCard(dto.Id, dto.Name, subtypes, rules, dto.Set.Id, dto.Images.Small,
                PokemonType.Colorless, SpecialEnergyType.DoubleColorless);
        if (dto.Name.Contains("Rainbow", StringComparison.OrdinalIgnoreCase))
            return new EnergyCard(dto.Id, dto.Name, subtypes, rules, dto.Set.Id, dto.Images.Small,
                PokemonType.Colorless, SpecialEnergyType.Rainbow);
        var typeName = dto.Name.Replace(" Energy", string.Empty, StringComparison.OrdinalIgnoreCase);
        return new EnergyCard(dto.Id, dto.Name, subtypes, rules, dto.Set.Id, dto.Images.Small,
            MapType(typeName), Special: null);
    }

    private static SubType MapSubType(string subtype) => subtype switch
    {
        "Basic" => SubType.Basic,
        "Stage 1" => SubType.Stage1,
        "Stage 2" => SubType.Stage2,
        "EX" => SubType.Ex,
        "MEGA" => SubType.Mega,
        "Item" => SubType.Item,
        "Supporter" => SubType.Supporter,
        "Stadium" => SubType.Stadium,
        "Pokémon Tool" => SubType.Tool,
        "ACE SPEC" => SubType.AceSpec,
        _ => throw new InvalidOperationException($"Unknown subtype '{subtype}'."),
    };

    private static SubType MapTrainerSubtype(List<string> subtypes)
    {
        if (subtypes.Contains("Supporter", StringComparer.Ordinal))
            return SubType.Supporter;
        if (subtypes.Contains("Stadium", StringComparer.Ordinal))
            return SubType.Stadium;
        if (subtypes.Contains("Pokémon Tool", StringComparer.Ordinal))
            return SubType.Tool;
        if (subtypes.Contains("ACE SPEC", StringComparer.Ordinal))
            return SubType.AceSpec;
        return SubType.Item;
    }

    private static PokemonType MapType(string type) => type switch
    {
        "Grass" => PokemonType.Grass,
        "Fire" => PokemonType.Fire,
        "Water" => PokemonType.Water,
        "Lightning" => PokemonType.Lightning,
        "Psychic" => PokemonType.Psychic,
        "Fighting" => PokemonType.Fighting,
        "Darkness" => PokemonType.Darkness,
        "Metal" => PokemonType.Metal,
        "Fairy" => PokemonType.Fairy,
        "Dragon" => PokemonType.Dragon,
        "Colorless" => PokemonType.Colorless,
        _ => throw new InvalidOperationException($"Unknown pokemon type '{type}'."),
    };

    private static AbilityType MapAbilityType(string type) => type switch
    {
        "Ancient Trait" => AbilityType.AncientTrait,
        "Ability" => AbilityType.Activated,
        "Poké-Power" or "Poké-Body" => AbilityType.Triggered,
        _ => AbilityType.Passive,
    };

    private static int ParseHp(string? hp)
    {
        if (string.IsNullOrWhiteSpace(hp))
            return 0;
        var digits = new string(hp.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : 0;
    }
}
