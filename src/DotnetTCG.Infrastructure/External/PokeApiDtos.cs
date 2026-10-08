using System.Text.Json.Serialization;

namespace DotnetTCG.Infrastructure.External;

internal sealed record PokeApiResponse(
    [property: JsonPropertyName("data")] List<PokeCardDto> Data,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("totalCount")] int TotalCount);

internal sealed record PokeCardDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("supertype")] string Supertype,
    [property: JsonPropertyName("subtypes")] List<string> Subtypes,
    [property: JsonPropertyName("hp")] string? Hp,
    [property: JsonPropertyName("types")] List<string>? Types,
    [property: JsonPropertyName("evolvesFrom")] string? EvolvesFrom,
    [property: JsonPropertyName("retreatCost")] List<string>? RetreatCost,
    [property: JsonPropertyName("rules")] List<string>? Rules,
    [property: JsonPropertyName("attacks")] List<PokeAttackDto>? Attacks,
    [property: JsonPropertyName("weaknesses")] List<PokeWeaknessDto>? Weaknesses,
    [property: JsonPropertyName("resistances")] List<PokeResistanceDto>? Resistances,
    [property: JsonPropertyName("abilities")] List<PokeAbilityDto>? Abilities,
    [property: JsonPropertyName("set")] PokeSetDto Set,
    [property: JsonPropertyName("images")] PokeImagesDto Images);

internal sealed record PokeAttackDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("cost")] List<string> Cost,
    [property: JsonPropertyName("damage")] string Damage,
    [property: JsonPropertyName("text")] string Text);

internal sealed record PokeWeaknessDto(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("value")] string Value);

internal sealed record PokeResistanceDto(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("value")] string Value);

internal sealed record PokeAbilityDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("type")] string Type);

internal sealed record PokeSetDto([property: JsonPropertyName("id")] string Id);

internal sealed record PokeImagesDto([property: JsonPropertyName("small")] string Small);
