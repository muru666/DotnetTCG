using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Domain.Cards;

public sealed record EnergyCard(
    string Id,
    string Name,
    IReadOnlyList<SubType> Subtypes,
    IReadOnlyList<string> Rules,
    string SetId,
    string ImageUrl,
    PokemonType EnergyType,
    SpecialEnergyType? Special)
    : Card(Id, Name, SuperType.Energy, Subtypes, Rules, SetId, ImageUrl)
{
    public bool IsBasic => Special is null;

    public int EnergyValue => Special == SpecialEnergyType.DoubleColorless ? 2 : 1;
}

public sealed record TrainerCard(
    string Id,
    string Name,
    IReadOnlyList<SubType> Subtypes,
    IReadOnlyList<string> Rules,
    string SetId,
    string ImageUrl,
    SubType TrainerSubtype)
    : Card(Id, Name, SuperType.Trainer, Subtypes, Rules, SetId, ImageUrl);
