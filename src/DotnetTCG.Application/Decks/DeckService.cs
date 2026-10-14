using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Common;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;
using Microsoft.Extensions.Logging;

namespace DotnetTCG.Application.Decks;

public sealed record DeckCardDto(string CardId, string CardName, int Quantity);

public sealed record DeckDto(
    Guid Id,
    string Name,
    bool IsValid,
    int Version,
    IReadOnlyList<DeckCardDto> Cards);

public sealed record DeckValidationResult(bool Valid, IReadOnlyList<string> Errors);

public sealed class DeckService(
    IDeckRepository decks,
    ICardRepository cards,
    RuleValidator rules,
    ILogger<DeckService>? logger = null)
{
    public const string StarterDeckName = "Mazo inicial";

    /// <summary>
    /// Crea el mazo inicial balanceado (20 Pokémon / 20 Entrenadores / 20 Energías).
    /// Nunca lanza: si el catálogo no alcanza, no hay mazo pero el registro sigue válido.
    /// </summary>
    public async Task<DeckEntry?> EnsureStarterDeckAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        try
        {
            if ((await decks.ListByOwnerAsync(ownerId, cancellationToken)).Count > 0)
                return null;

            var catalog = await cards.SearchAsync(null, null, 0, 500, cancellationToken);
            var pokemon = catalog
                .OfType<PokemonCard>()
                .Where(c => c.IsBasic)
                .GroupBy(c => c.Name)
                .Take(5)
                .Select(g => g.First().Id)
                .ToList();
            var trainers = catalog
                .OfType<TrainerCard>()
                .Where(c => c.TrainerSubtype != SubType.AceSpec)
                .GroupBy(c => c.Name)
                .Take(5)
                .Select(g => g.First().Id)
                .ToList();
            var energy = catalog
                .OfType<EnergyCard>()
                .FirstOrDefault(c => c.IsBasic)?.Id;
            if (pokemon.Count < 5 || trainers.Count < 5 || energy is null)
            {
                logger?.LogWarning("Catálogo insuficiente para mazo inicial.");
                return null;
            }

            var merged = new Dictionary<string, int>();
            foreach (var id in pokemon.Concat(trainers))
                merged[id] = 4;
            merged[energy] = 20;

            var domainCards = new List<Card>();
            foreach (var (cardId, quantity) in merged)
            {
                var card = await cards.GetByIdAsync(cardId, cancellationToken);
                if (card is null)
                    return null;
                for (var i = 0; i < quantity; i++)
                    domainCards.Add(card);
            }

            if (rules.ValidateDeck(domainCards).Count > 0)
            {
                logger?.LogWarning("Mazo inicial generado inválido; no se guarda.");
                return null;
            }

            var entry = new DeckEntry(Guid.NewGuid(), ownerId, StarterDeckName,
                IsValid: true, Version: 1, merged);
            await decks.AddAsync(entry, cancellationToken);
            return entry;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "No se pudo crear el mazo inicial.");
            return null;
        }
    }

    public async Task<Result<DeckDto>> CreateAsync(
        Guid ownerId, string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result<DeckDto>.Failure(new Error("Deck.NameRequired", "Name is required."));
        if (await decks.FindByOwnerAndNameAsync(ownerId, name, cancellationToken) is not null)
            return Result<DeckDto>.Failure(new Error("Deck.DuplicateName", "Deck name already exists."));

        var entry = new DeckEntry(Guid.NewGuid(), ownerId, name, IsValid: false, Version: 1,
            new Dictionary<string, int>());
        await decks.AddAsync(entry, cancellationToken);
        return Result<DeckDto>.Success(await ToDtoAsync(entry, cancellationToken));
    }

    public async Task<IReadOnlyList<DeckDto>> ListAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        var entries = await decks.ListByOwnerAsync(ownerId, cancellationToken);
        var dtos = new List<DeckDto>();
        foreach (var entry in entries)
            dtos.Add(await ToDtoAsync(entry, cancellationToken));
        return dtos;
    }

    public async Task<Result<DeckDto>> GetAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
    {
        var entry = await OwnedAsync(id, ownerId, cancellationToken);
        return entry is null
            ? Result<DeckDto>.Failure(new Error("Deck.NotFound", "Deck not found."))
            : Result<DeckDto>.Success(await ToDtoAsync(entry, cancellationToken));
    }

    public async Task<Result<DeckDto>> RenameAsync(
        Guid id, Guid ownerId, string name, CancellationToken cancellationToken)
    {
        var entry = await OwnedAsync(id, ownerId, cancellationToken);
        if (entry is null)
            return Result<DeckDto>.Failure(new Error("Deck.NotFound", "Deck not found."));
        if (string.IsNullOrWhiteSpace(name))
            return Result<DeckDto>.Failure(new Error("Deck.NameRequired", "Name is required."));

        var updated = entry with { Name = name };
        await decks.UpdateAsync(updated, cancellationToken);
        return Result<DeckDto>.Success(await ToDtoAsync(updated with { Version = entry.Version + 1 }, cancellationToken));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
    {
        var entry = await OwnedAsync(id, ownerId, cancellationToken);
        if (entry is null)
            return Result<bool>.Failure(new Error("Deck.NotFound", "Deck not found."));
        await decks.DeleteAsync(id, cancellationToken);
        return Result<bool>.Success(true);
    }

    public async Task<Result<DeckDto>> AddCardAsync(
        Guid id, Guid ownerId, string cardId, int quantity, CancellationToken cancellationToken)
    {
        if (quantity is < 1 or > RuleValidator.RequiredDeckSize)
            return Result<DeckDto>.Failure(new Error("Deck.BadQuantity", "Quantity must be 1-60."));
        var entry = await OwnedAsync(id, ownerId, cancellationToken);
        if (entry is null)
            return Result<DeckDto>.Failure(new Error("Deck.NotFound", "Deck not found."));
        if (await cards.GetByIdAsync(cardId, cancellationToken) is null)
            return Result<DeckDto>.Failure(new Error("Card.NotFound", $"Card '{cardId}' not found."));

        var merged = new Dictionary<string, int>(entry.Cards);
        merged[cardId] = merged.GetValueOrDefault(cardId) + quantity;
        if (merged.Values.Sum() > RuleValidator.RequiredDeckSize)
            return Result<DeckDto>.Failure(new Error("Deck.TooMany", "Deck cannot exceed 60 cards."));

        return await SaveAsync(entry, merged, cancellationToken);
    }

    public async Task<Result<DeckDto>> RemoveCardAsync(
        Guid id, Guid ownerId, string cardId, CancellationToken cancellationToken)
    {
        var entry = await OwnedAsync(id, ownerId, cancellationToken);
        if (entry is null)
            return Result<DeckDto>.Failure(new Error("Deck.NotFound", "Deck not found."));

        var merged = new Dictionary<string, int>(entry.Cards);
        merged.Remove(cardId);
        return await SaveAsync(entry, merged, cancellationToken);
    }

    public async Task<Result<DeckDto>> SetQuantityAsync(
        Guid id, Guid ownerId, string cardId, int quantity, CancellationToken cancellationToken)
    {
        if (quantity is < 1 or > RuleValidator.RequiredDeckSize)
            return Result<DeckDto>.Failure(new Error("Deck.BadQuantity", "Quantity must be 1-60."));
        var entry = await OwnedAsync(id, ownerId, cancellationToken);
        if (entry is null)
            return Result<DeckDto>.Failure(new Error("Deck.NotFound", "Deck not found."));
        if (!entry.Cards.ContainsKey(cardId))
            return Result<DeckDto>.Failure(new Error("Card.NotFound", $"Card '{cardId}' not in deck."));

        var merged = new Dictionary<string, int>(entry.Cards) { [cardId] = quantity };
        if (merged.Values.Sum() > RuleValidator.RequiredDeckSize)
            return Result<DeckDto>.Failure(new Error("Deck.TooMany", "Deck cannot exceed 60 cards."));

        return await SaveAsync(entry, merged, cancellationToken);
    }

    public async Task<Result<DeckValidationResult>> ValidateAsync(
        Guid id, Guid ownerId, CancellationToken cancellationToken)
    {
        var entry = await OwnedAsync(id, ownerId, cancellationToken);
        if (entry is null)
            return Result<DeckValidationResult>.Failure(new Error("Deck.NotFound", "Deck not found."));

        var errors = new List<string>();
        var domainCards = new List<Card>();
        foreach (var (cardId, quantity) in entry.Cards)
        {
            var card = await cards.GetByIdAsync(cardId, cancellationToken);
            if (card is null)
            {
                errors.Add($"Card '{cardId}' no longer exists.");
                continue;
            }

            for (var i = 0; i < quantity; i++)
                domainCards.Add(card);
        }

        errors.AddRange(rules.ValidateDeck(domainCards).Select(e => e.Message));

        var aceSpec = domainCards.OfType<TrainerCard>()
            .Count(t => t.TrainerSubtype == SubType.AceSpec);
        if (aceSpec > 1)
            errors.Add("At most one ACE SPEC card per deck.");

        var valid = errors.Count == 0;
        await decks.UpdateAsync(entry with { IsValid = valid }, cancellationToken);
        return Result<DeckValidationResult>.Success(new DeckValidationResult(valid, errors));
    }

    private async Task<DeckEntry?> OwnedAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
    {
        var entry = await decks.GetByIdAsync(id, cancellationToken);
        return entry is null || entry.OwnerId != ownerId ? null : entry;
    }

    private async Task<Result<DeckDto>> SaveAsync(
        DeckEntry entry, Dictionary<string, int> merged, CancellationToken cancellationToken)
    {
        var updated = entry with { Cards = merged, IsValid = false };
        await decks.UpdateAsync(updated, cancellationToken);
        return Result<DeckDto>.Success(await ToDtoAsync(updated with { Version = entry.Version + 1 }, cancellationToken));
    }

    private async Task<DeckDto> ToDtoAsync(DeckEntry entry, CancellationToken cancellationToken)
    {
        var items = new List<DeckCardDto>();
        foreach (var (cardId, quantity) in entry.Cards)
        {
            var card = await cards.GetByIdAsync(cardId, cancellationToken);
            items.Add(new DeckCardDto(cardId, card?.Name ?? cardId, quantity));
        }

        return new DeckDto(entry.Id, entry.Name, entry.IsValid, entry.Version, items);
    }
}
