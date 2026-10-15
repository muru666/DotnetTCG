using DotnetTCG.Application.Cards;
using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Domain.Common;
using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Application.Cards;

public sealed class CardService(ICardRepository cards, IPokeApiClient pokeApi)
{
    public const int MaxPageSize = 100;

    public async Task<PagedResult<CardDto>> SearchAsync(
        string? name, PokemonType? type, SuperType? supertype,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(0, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var total = await cards.CountAsync(name, type, supertype, cancellationToken);
        var items = await cards.SearchAsync(name, type, supertype, page, pageSize, cancellationToken);
        return new PagedResult<CardDto>(items.Select(CardDto.FromDomain).ToList(), total, page, pageSize);
    }

    public async Task<Result<CardDto>> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var card = await cards.GetByIdAsync(id, cancellationToken);
        return card is null
            ? Result<CardDto>.Failure(new Error("Card.NotFound", $"Card '{id}' not found."))
            : Result<CardDto>.Success(CardDto.FromDomain(card));
    }

    public async Task<Result<int>> ImportSetAsync(string setId, CancellationToken cancellationToken)
    {
        var fetched = await pokeApi.FetchSetAsync(setId, cancellationToken);
        await cards.UpsertRangeAsync(fetched, cancellationToken);
        return Result<int>.Success(fetched.Count);
    }
}
