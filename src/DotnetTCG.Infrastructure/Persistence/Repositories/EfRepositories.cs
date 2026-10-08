using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Application.Decks;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Infrastructure.Mapping;
using DotnetTCG.Infrastructure.Persistence;
using DotnetTCG.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DotnetTCG.Infrastructure.Persistence.Repositories;

public sealed class EfCardRepository(AppDbContext db) : ICardRepository
{
    public async Task<Card?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await db.Cards.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return entity is null ? null : CardMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<Card>> SearchAsync(
        string? name, PokemonType? type, int page, int pageSize, CancellationToken cancellationToken)
    {
        var entities = await ApplyFilter(db.Cards.AsNoTracking(), name, type)
            .OrderBy(c => c.Id)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return entities.Select(CardMapper.ToDomain).ToList();
    }

    public Task<int> CountAsync(string? name, PokemonType? type, CancellationToken cancellationToken)
        => ApplyFilter(db.Cards.AsNoTracking(), name, type).CountAsync(cancellationToken);

    public async Task UpsertRangeAsync(IEnumerable<Card> cards, CancellationToken cancellationToken)
    {
        foreach (var card in cards)
        {
            var entity = CardMapper.ToEntity(card);
            var existing = await db.Cards.FindAsync([entity.Id], cancellationToken);
            if (existing is null)
                db.Cards.Add(entity);
            else
                db.Entry(existing).CurrentValues.SetValues(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CountBySetAsync(string setId, CancellationToken cancellationToken)
        => db.Cards.AsNoTracking().CountAsync(c => c.SetId == setId, cancellationToken);

    private static IQueryable<CardEntity> ApplyFilter(
        IQueryable<CardEntity> query, string? name, PokemonType? type)
    {
        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(c => EF.Functions.ILike(c.Name, $"%{name}%"));
        if (type is not null)
        {
            var typeValue = (int)type.Value;
            query = query.Where(c =>
                c.Supertype == (int)SuperType.Pokemon && c.PokemonType == typeValue
                || c.Supertype == (int)SuperType.Energy && c.EnergyType == typeValue);
        }

        return query;
    }
}

public sealed class EfDeckRepository(AppDbContext db) : IDeckRepository
{
    public async Task<DeckEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.Decks.AsNoTracking()
            .Include(d => d.Cards)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        return entity is null ? null : ToEntry(entity);
    }

    public async Task<IReadOnlyList<DeckEntry>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        var entities = await db.Decks.AsNoTracking()
            .Include(d => d.Cards)
            .Where(d => d.OwnerId == ownerId)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);
        return entities.Select(ToEntry).ToList();
    }

    public async Task<DeckEntry?> FindByOwnerAndNameAsync(
        Guid ownerId, string name, CancellationToken cancellationToken)
    {
        var entity = await db.Decks.AsNoTracking()
            .Include(d => d.Cards)
            .FirstOrDefaultAsync(d => d.OwnerId == ownerId && d.Name == name, cancellationToken);
        return entity is null ? null : ToEntry(entity);
    }

    public async Task AddAsync(DeckEntry deck, CancellationToken cancellationToken)
    {
        db.Decks.Add(ToEntity(deck, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(DeckEntry deck, CancellationToken cancellationToken)
    {
        var entity = await db.Decks.Include(d => d.Cards)
            .FirstOrDefaultAsync(d => d.Id == deck.Id, cancellationToken);
        if (entity is null)
            throw new InvalidOperationException($"Deck {deck.Id} not found.");

        entity.Name = deck.Name;
        entity.IsValid = deck.IsValid;
        entity.Version++;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        db.DeckCards.RemoveRange(entity.Cards);
        entity.Cards = deck.Cards
            .Select(c => new DeckCardEntity { DeckId = deck.Id, CardId = c.Key, Quantity = c.Value })
            .ToList();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await db.Decks.Where(d => d.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    private static DeckEntry ToEntry(DeckEntity entity) => new(
        entity.Id,
        entity.OwnerId,
        entity.Name,
        entity.IsValid,
        entity.Version,
        entity.Cards.ToDictionary(c => c.CardId, c => c.Quantity));

    private static DeckEntity ToEntity(DeckEntry deck, DateTimeOffset now) => new()
    {
        Id = deck.Id,
        OwnerId = deck.OwnerId,
        Name = deck.Name,
        IsValid = deck.IsValid,
        Version = deck.Version,
        CreatedAt = now,
        UpdatedAt = now,
        Cards = deck.Cards
            .Select(c => new DeckCardEntity { DeckId = deck.Id, CardId = c.Key, Quantity = c.Value })
            .ToList(),
    };
}
