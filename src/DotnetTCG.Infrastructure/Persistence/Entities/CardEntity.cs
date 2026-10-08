namespace DotnetTCG.Infrastructure.Persistence.Entities;

public sealed class CardEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Supertype { get; set; }
    public string Subtypes { get; set; } = string.Empty;
    public string SetId { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;

    public int? Hp { get; set; }
    public int? RetreatCost { get; set; }
    public string? EvolvesFrom { get; set; }
    public int? PokemonType { get; set; }
    public string? AttacksJson { get; set; }
    public string? AbilitiesJson { get; set; }
    public string? WeaknessesJson { get; set; }
    public string? ResistancesJson { get; set; }

    public int? EnergyType { get; set; }
    public int? SpecialEnergy { get; set; }

    public int? TrainerSubtype { get; set; }
}
