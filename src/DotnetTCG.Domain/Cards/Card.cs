using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Domain.Cards;

public abstract record Card(
    string Id,
    string Name,
    SuperType Supertype,
    IReadOnlyList<SubType> Subtypes,
    IReadOnlyList<string> Rules,
    string SetId,
    string ImageUrl);
