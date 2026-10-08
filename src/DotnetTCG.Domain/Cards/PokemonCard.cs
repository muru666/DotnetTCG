using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Domain.Cards;

public sealed record Attack(
    string Name,
    string Description,
    string Damage,
    IReadOnlyList<PokemonType> Cost);

public sealed record PokemonAbility(
    string Name,
    string Text,
    AbilityType Type);

public sealed record Weakness(
    PokemonType Type,
    string Multiplier);

public sealed record Resistance(
    PokemonType Type,
    string Reduction);

public sealed record PokemonCard(
    string Id,
    string Name,
    IReadOnlyList<SubType> Subtypes,
    IReadOnlyList<string> Rules,
    string SetId,
    string ImageUrl,
    int Hp,
    int RetreatCost,
    string? EvolvesFrom,
    PokemonType PokemonType,
    IReadOnlyList<Attack> Attacks,
    IReadOnlyList<PokemonAbility> Abilities,
    IReadOnlyList<Weakness> Weaknesses,
    IReadOnlyList<Resistance> Resistances)
    : Card(Id, Name, SuperType.Pokemon, Subtypes, Rules, SetId, ImageUrl)
{
    public bool IsBasic => Subtypes.Contains(SubType.Basic);
    public bool IsEx => Subtypes.Contains(SubType.Ex);
}
