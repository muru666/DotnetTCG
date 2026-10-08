namespace DotnetTCG.Application.Decks;

public sealed record DeckEntry(
    Guid Id,
    Guid OwnerId,
    string Name,
    bool IsValid,
    int Version,
    IReadOnlyDictionary<string, int> Cards);
