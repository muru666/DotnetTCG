using System.Net;
using System.Text.Json;
using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Domain.Cards;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotnetTCG.Infrastructure.External;

public sealed class PokeApiClient(
    HttpClient httpClient,
    IOptions<PokeApiOptions> options,
    ILogger<PokeApiClient> logger) : IPokeApiClient
{
    public async Task<IReadOnlyList<Card>> FetchSetAsync(string setId, CancellationToken cancellationToken)
    {
        var pageSize = options.Value.PageSize;
        var cards = new List<Card>();
        var page = 1;

        while (true)
        {
            var url = $"cards?q=set.id:{setId}&page={page}&pageSize={pageSize}&orderBy=number";
            var response = await GetWithRetryAsync(url, cancellationToken);
            var pageDto = JsonSerializer.Deserialize<PokeApiResponse>(
                response, new JsonSerializerOptions { PropertyNameCaseInsensitive = false });
            if (pageDto is null || pageDto.Data.Count == 0)
                break;

            cards.AddRange(pageDto.Data.Select(PokeCardMapper.ToDomain));
            logger.LogInformation("Fetched {Count} cards from {SetId} (page {Page})", pageDto.Data.Count, setId, page);

            if (cards.Count >= pageDto.TotalCount || pageDto.Data.Count < pageSize)
                break;
            page++;
        }

        return cards;
    }

    private async Task<string> GetWithRetryAsync(string url, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrWhiteSpace(options.Value.ApiKey))
                    request.Headers.Add("X-Api-Key", options.Value.ApiKey);

                using var response = await httpClient.SendAsync(request, cancellationToken);
                if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
                    throw new HttpRequestException($"Transient status {(int)response.StatusCode}.");

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && attempt < 3)
            {
                logger.LogWarning(ex, "PokeAPI request failed (attempt {Attempt}). Retrying.", attempt);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
            }
        }

        throw new HttpRequestException("PokeAPI request failed after 3 attempts.");
    }
}
