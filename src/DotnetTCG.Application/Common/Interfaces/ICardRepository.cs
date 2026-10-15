using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Application.Common.Interfaces;

public interface ICardRepository
{
    Task<Card?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<string, Card>> GetByIdsAsync(
        IEnumerable<string> ids, CancellationToken cancellationToken);
    Task<IReadOnlyList<Card>> SearchAsync(
        string? name, PokemonType? type, SuperType? supertype,
        int page, int pageSize, CancellationToken cancellationToken);
    Task<int> CountAsync(string? name, PokemonType? type, SuperType? supertype, CancellationToken cancellationToken);
    Task UpsertRangeAsync(IEnumerable<Card> cards, CancellationToken cancellationToken);
    Task<int> CountBySetAsync(string setId, CancellationToken cancellationToken);
}
