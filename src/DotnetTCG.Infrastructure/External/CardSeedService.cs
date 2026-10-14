using System.Text.Json;
using DotnetTCG.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace DotnetTCG.Infrastructure.External;

/// <summary>
/// Siembra el catálogo desde un JSON local con forma de respuesta de pokemontcg.io
/// (ver <c>data/xy1.json</c>). Reutiliza el mismo mapeo que el importador en vivo.
/// </summary>
public sealed class CardSeedService(
    ICardRepository cards,
    ILogger<CardSeedService> logger)
{
    public async Task<int> SeedFromFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var response = await JsonSerializer.DeserializeAsync<PokeApiResponse>(
            stream, cancellationToken: cancellationToken);
        if (response is null || response.Data.Count == 0)
            throw new InvalidOperationException($"Seed file '{path}' is empty or invalid.");

        var mapped = response.Data.Select(PokeCardMapper.ToDomain).ToList();
        await cards.UpsertRangeAsync(mapped, cancellationToken);
        logger.LogInformation("Seeded {Count} cards from {Path}", mapped.Count, path);
        return mapped.Count;
    }
}
