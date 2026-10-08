using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Common;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

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
    RuleValidator rules)
{
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
