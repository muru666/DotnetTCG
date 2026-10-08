using DotnetTCG.Domain.Cards;

namespace DotnetTCG.Application.Common.Interfaces;

public interface IPokeApiClient
{
    Task<IReadOnlyList<Card>> FetchSetAsync(string setId, CancellationToken cancellationToken);
}
