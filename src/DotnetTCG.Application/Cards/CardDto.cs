using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Application.Cards;

public sealed record CardDto(
    string Id,
    string Name,
    SuperType Supertype,
    IReadOnlyList<SubType> Subtypes,
    string SetId,
    string ImageUrl,
    int? Hp,
    int? RetreatCost,
    string? EvolvesFrom,
    PokemonType? PokemonType,
    IReadOnlyList<Attack> Attacks,
    IReadOnlyList<PokemonAbility> Abilities,
    IReadOnlyList<Weakness> Weaknesses,
    IReadOnlyList<Resistance> Resistances,
    PokemonType? EnergyType,
    SpecialEnergyType? SpecialEnergy,
    SubType? TrainerSubtype)
{
    public static CardDto FromDomain(Card card) => card switch
    {
        PokemonCard p => new CardDto(p.Id, p.Name, p.Supertype, p.Subtypes, p.SetId, p.ImageUrl,
            p.Hp, p.RetreatCost, p.EvolvesFrom, p.PokemonType, p.Attacks, p.Abilities,
            p.Weaknesses, p.Resistances, null, null, null),
        EnergyCard e => new CardDto(e.Id, e.Name, e.Supertype, e.Subtypes, e.SetId, e.ImageUrl,
            null, null, null, null, [], [], [], [], e.EnergyType, e.Special, null),
        TrainerCard t => new CardDto(t.Id, t.Name, t.Supertype, t.Subtypes, t.SetId, t.ImageUrl,
            null, null, null, null, [], [], [], [], null, null, t.TrainerSubtype),
        _ => throw new ArgumentOutOfRangeException(nameof(card)),
    };
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
