using DotnetTCG.Application.Decks;

namespace DotnetTCG.Application.Common.Interfaces;

public interface IDeckRepository
{
    Task<DeckEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeckEntry>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken);
    Task<DeckEntry?> FindByOwnerAndNameAsync(Guid ownerId, string name, CancellationToken cancellationToken);
    Task AddAsync(DeckEntry deck, CancellationToken cancellationToken);
    Task UpdateAsync(DeckEntry deck, CancellationToken cancellationToken);
    /// <summary>Devuelve false si el mazo está referenciado por una partida (FK).</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
