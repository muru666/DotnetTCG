namespace DotnetTCG.Infrastructure.External;

public sealed class PokeApiOptions
{
    public const string SectionName = "PokeApi";

    public string BaseUrl { get; init; } = "https://api.pokemontcg.io/v2/";
    public string? ApiKey { get; init; }
    public int PageSize { get; init; } = 250;
}
