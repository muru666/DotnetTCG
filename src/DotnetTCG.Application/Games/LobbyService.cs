using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Domain.Common;
using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Application.Games;

public sealed record LobbyPlayerDto(Guid UserId, int Order, Guid? DeckId, bool IsReady, string Username);

public sealed record LobbyDto(
    Guid Id,
    Guid CreatorId,
    bool IsPublic,
    bool AcceptsSpectators,
    IReadOnlyList<LobbyPlayerDto> Players);

public sealed record LeaveResult(bool Deleted, LobbyDto? Lobby);

public sealed class LobbyService(
    IGameRepository games,
    IDeckRepository decks,
    IPasswordHasher hasher,
    IClock clock)
{
    public async Task<Result<LobbyDto>> CreateAsync(
        Guid creatorId, bool isPublic, bool acceptsSpectators, string? password,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var game = new GameData(
            Guid.NewGuid(), GameStatus.InLobby, 0, 0, TurnPhase.Draw, 0, false, null,
            creatorId, isPublic, acceptsSpectators,
            string.IsNullOrWhiteSpace(password) ? null : hasher.Hash(password),
            null, 0,
            [new GamePlayerData(creatorId, 0, null, false, Username: string.Empty)],
            now);
        await games.AddAsync(game, cancellationToken);
        return Result<LobbyDto>.Success(ToDto(game));
    }

    public async Task<Result<LobbyDto>> JoinAsync(
        Guid gameId, Guid userId, string? password, CancellationToken cancellationToken)
    {
        var game = await games.GetByIdAsync(gameId, cancellationToken);
        if (game is null)
            return Result<LobbyDto>.Failure(new Error("Game.NotFound", "Game not found."));
        if (game.Status != GameStatus.InLobby)
            return Result<LobbyDto>.Failure(new Error("Game.NotJoinable", "Game already started."));
        if (game.Players.Any(p => p.UserId == userId))
            return Result<LobbyDto>.Success(ToDto(game));
        if (game.Players.Count >= 2)
            return Result<LobbyDto>.Failure(new Error("Game.Full", "Lobby is full."));
        if (game.PasswordHash is not null
            && (password is null || !hasher.Verify(password, game.PasswordHash)))
            return Result<LobbyDto>.Failure(new Error("Game.WrongPassword", "Wrong password."));

        var updated = game with
        {
            Players = [.. game.Players, new GamePlayerData(userId, 1, null, false, Username: string.Empty)],
            UpdatedAt = clock.UtcNow,
        };
        return await SaveAsync(updated, cancellationToken);
    }

    public async Task<Result<LobbyDto>> SelectDeckAsync(
        Guid gameId, Guid userId, Guid deckId, CancellationToken cancellationToken)
    {
        var game = await games.GetByIdAsync(gameId, cancellationToken);
        if (game is null || game.Players.All(p => p.UserId != userId))
            return Result<LobbyDto>.Failure(new Error("Game.NotFound", "Game not found."));
        if (game.Status != GameStatus.InLobby)
            return Result<LobbyDto>.Failure(new Error("Game.NotJoinable", "Game already started."));

        var deck = await decks.GetByIdAsync(deckId, cancellationToken);
        if (deck is null || deck.OwnerId != userId)
            return Result<LobbyDto>.Failure(new Error("Deck.NotFound", "Deck not found."));
        if (!deck.IsValid)
            return Result<LobbyDto>.Failure(new Error("Deck.Invalid", "Deck is not valid."));

        var updated = game with
        {
            Players = game.Players
                .Select(p => p.UserId == userId ? p with { DeckId = deckId, IsReady = false } : p)
                .ToList(),
            UpdatedAt = clock.UtcNow,
        };
        return await SaveAsync(updated, cancellationToken);
    }

    public async Task<Result<LobbyDto>> SetReadyAsync(
        Guid gameId, Guid userId, bool ready, CancellationToken cancellationToken)
    {
        var game = await games.GetByIdAsync(gameId, cancellationToken);
        if (game is null || game.Players.All(p => p.UserId != userId))
            return Result<LobbyDto>.Failure(new Error("Game.NotFound", "Game not found."));
        if (game.Status != GameStatus.InLobby)
            return Result<LobbyDto>.Failure(new Error("Game.NotJoinable", "Game already started."));

        var player = game.Players.First(p => p.UserId == userId);
        if (ready && player.DeckId is null)
            return Result<LobbyDto>.Failure(new Error("Game.NoDeck", "Select a deck first."));

        var updated = game with
        {
            Players = game.Players
                .Select(p => p.UserId == userId ? p with { IsReady = ready } : p)
                .ToList(),
            UpdatedAt = clock.UtcNow,
        };
        return await SaveAsync(updated, cancellationToken);
    }

    public async Task<Result<LobbyDto>> RemovePlayerAsync(
        Guid gameId, Guid creatorId, Guid targetUserId, CancellationToken cancellationToken)
    {
        var game = await games.GetByIdAsync(gameId, cancellationToken);
        if (game is null)
            return Result<LobbyDto>.Failure(new Error("Game.NotFound", "Game not found."));
        if (game.CreatorId != creatorId)
            return Result<LobbyDto>.Failure(new Error("Game.Forbidden", "Only the creator can remove players."));
        if (game.Status != GameStatus.InLobby)
            return Result<LobbyDto>.Failure(new Error("Game.NotJoinable", "Game already started."));
        if (targetUserId == creatorId || game.Players.All(p => p.UserId != targetUserId))
            return Result<LobbyDto>.Failure(new Error("Game.NotFound", "Player not in lobby."));

        var updated = game with
        {
            Players = game.Players.Where(p => p.UserId != targetUserId).ToList(),
            UpdatedAt = clock.UtcNow,
        };
        return await SaveAsync(updated, cancellationToken);
    }

    public async Task<Result<LeaveResult>> LeaveAsync(
        Guid gameId, Guid userId, CancellationToken cancellationToken)
    {
        var game = await games.GetByIdAsync(gameId, cancellationToken);
        if (game is null || game.Players.All(p => p.UserId != userId))
            return Result<LeaveResult>.Failure(new Error("Game.NotFound", "Game not found."));

        var remaining = game.Players.Where(p => p.UserId != userId).ToList();
        if (remaining.Count == 0)
        {
            await games.DeleteAsync(gameId, cancellationToken);
            return Result<LeaveResult>.Success(new LeaveResult(true, null));
        }

        var updated = game with
        {
            Players = remaining,
            CreatorId = game.CreatorId == userId ? remaining[0].UserId : game.CreatorId,
            UpdatedAt = clock.UtcNow,
        };
        var saved = await SaveAsync(updated, cancellationToken);
        return saved.IsSuccess
            ? Result<LeaveResult>.Success(new LeaveResult(false, saved.Value))
            : Result<LeaveResult>.Failure(saved.Error);
    }

    public async Task<Result<LobbyDto>> UpdateSettingsAsync(
        Guid gameId, Guid creatorId, bool isPublic, bool acceptsSpectators, string? password,
        CancellationToken cancellationToken)
    {
        var game = await games.GetByIdAsync(gameId, cancellationToken);
        if (game is null)
            return Result<LobbyDto>.Failure(new Error("Game.NotFound", "Game not found."));
        if (game.CreatorId != creatorId)
            return Result<LobbyDto>.Failure(new Error("Game.Forbidden", "Only the creator can change settings."));
        if (game.Status != GameStatus.InLobby)
            return Result<LobbyDto>.Failure(new Error("Game.NotJoinable", "Game already started."));

        var updated = game with
        {
            IsPublic = isPublic,
            AcceptsSpectators = acceptsSpectators,
            PasswordHash = password is null
                ? game.PasswordHash
                : string.IsNullOrWhiteSpace(password) ? null : hasher.Hash(password),
            UpdatedAt = clock.UtcNow,
        };
        return await SaveAsync(updated, cancellationToken);
    }

    public async Task<Result<LobbyDto>> GetAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var game = await games.GetByIdAsync(gameId, cancellationToken);
        return game is null
            ? Result<LobbyDto>.Failure(new Error("Game.NotFound", "Game not found."))
            : Result<LobbyDto>.Success(ToDto(game));
    }

    public async Task<IReadOnlyList<LobbyDto>> ListOpenAsync(CancellationToken cancellationToken)
    {
        var lobby = await games.ListLobbyAsync(cancellationToken);
        return lobby.Select(ToDto).ToList();
    }

    public async Task<int> CleanupStaleAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        var lobby = await games.ListLobbyAsync(cancellationToken);
        var count = 0;
        foreach (var game in lobby.Where(g => g.UpdatedAt < olderThan))
        {
            await games.DeleteAsync(game.Id, cancellationToken);
            count++;
        }

        return count;
    }

    private async Task<Result<LobbyDto>> SaveAsync(GameData game, CancellationToken cancellationToken)
    {
        try
        {
            await games.UpdateAsync(game, game.Version, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<LobbyDto>.Failure(new Error("Game.Conflict", "Lobby changed. Retry."));
        }

        var reloaded = await games.GetByIdAsync(game.Id, cancellationToken);
        return reloaded is null
            ? Result<LobbyDto>.Failure(new Error("Game.NotFound", "Game not found."))
            : Result<LobbyDto>.Success(ToDto(reloaded));
    }

    private static LobbyDto ToDto(GameData game) => new(
        game.Id,
        game.CreatorId,
        game.IsPublic,
        game.AcceptsSpectators,
        game.Players.Select(p => new LobbyPlayerDto(p.UserId, p.Order, p.DeckId, p.IsReady, p.Username)).ToList());
}
