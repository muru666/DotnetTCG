using System.Text.Json;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Infrastructure.Persistence.Entities;

namespace DotnetTCG.Infrastructure.Mapping;

public static class CardMapper
{
    public static CardEntity ToEntity(Card card)
    {
        var entity = BaseEntity(card);
        switch (card)
        {
            case PokemonCard p:
                entity.Hp = p.Hp;
                entity.RetreatCost = p.RetreatCost;
                entity.EvolvesFrom = p.EvolvesFrom;
                entity.PokemonType = (int)p.PokemonType;
                entity.AttacksJson = JsonSerializer.Serialize(p.Attacks);
                entity.AbilitiesJson = JsonSerializer.Serialize(p.Abilities);
                entity.WeaknessesJson = JsonSerializer.Serialize(p.Weaknesses);
                entity.ResistancesJson = JsonSerializer.Serialize(p.Resistances);
                break;
            case EnergyCard e:
                entity.EnergyType = (int)e.EnergyType;
                entity.SpecialEnergy = e.Special is null ? null : (int)e.Special.Value;
                break;
            case TrainerCard t:
                entity.TrainerSubtype = (int)t.TrainerSubtype;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(card));
        }

        return entity;
    }

    public static Card ToDomain(CardEntity entity)
    {
        var supertype = (SuperType)entity.Supertype;
        var subtypes = ParseSubtypes(entity.Subtypes);
        return supertype switch
        {
            SuperType.Pokemon => new PokemonCard(
                entity.Id, entity.Name, subtypes, [], entity.SetId, entity.ImageUrl,
                entity.Hp ?? 0, entity.RetreatCost ?? 0, entity.EvolvesFrom,
                (PokemonType)(entity.PokemonType ?? (int)PokemonType.Colorless),
                Deserialize<Attack>(entity.AttacksJson),
                Deserialize<PokemonAbility>(entity.AbilitiesJson),
                Deserialize<Weakness>(entity.WeaknessesJson),
                Deserialize<Resistance>(entity.ResistancesJson)),
            SuperType.Energy => new EnergyCard(
                entity.Id, entity.Name, subtypes, [], entity.SetId, entity.ImageUrl,
                (PokemonType)(entity.EnergyType ?? (int)PokemonType.Colorless),
                entity.SpecialEnergy is null ? null : (SpecialEnergyType)entity.SpecialEnergy.Value),
            SuperType.Trainer => new TrainerCard(
                entity.Id, entity.Name, subtypes, [], entity.SetId, entity.ImageUrl,
                entity.TrainerSubtype is null ? SubType.Item : (SubType)entity.TrainerSubtype.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(entity)),
        };
    }

    private static CardEntity BaseEntity(Card card) => new()
    {
        Id = card.Id,
        Name = card.Name,
        Supertype = (int)card.Supertype,
        Subtypes = string.Join(",", card.Subtypes.Select(s => (int)s)),
        SetId = card.SetId,
        ImageUrl = card.ImageUrl,
    };

    private static IReadOnlyList<SubType> ParseSubtypes(string subtypes)
        => string.IsNullOrWhiteSpace(subtypes)
            ? []
            : subtypes.Split(',').Where(s => int.TryParse(s, out _)).Select(s => (SubType)int.Parse(s)).ToList();

    private static IReadOnlyList<T> Deserialize<T>(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<T>>(json) ?? [];
}
