namespace DotnetTCG.Infrastructure.Persistence.Entities;

public sealed class DeckEntity
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public UserEntity? Owner { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<DeckCardEntity> Cards { get; set; } = [];
}

public sealed class DeckCardEntity
{
    public Guid DeckId { get; set; }
    public DeckEntity? Deck { get; set; }
    public string CardId { get; set; } = string.Empty;
    public CardEntity? Card { get; set; }
    public int Quantity { get; set; }
}
