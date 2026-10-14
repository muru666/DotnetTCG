using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Application.Decks;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Common;
using DotnetTCG.Domain.Engine;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Application.Games;

public sealed record TargetRef(string Zone, int Index);

public sealed record GameActionRequest(
    GameActionType Type,
    string? CardId = null,
    TargetRef? Target = null,
    string? AttackName = null,
    IReadOnlyList<string>? EnergyIdsToDiscard = null,
    string? ChoiceId = null,
    IReadOnlyList<string>? SelectedOptions = null,
    string? AbilityName = null);

public sealed record GameActionResultDto(
    bool Success,
    string? Error,
    PendingChoice? PendingChoice,
    IReadOnlyList<GameEvent> Events);

public sealed class GameExecutionService(
    IGameRepository games,
    IGameLogRepository logs,
    ICardRepository cards,
    IDeckRepository decks,
    GameEngine engine,
    IGameSnapshotSerializer snapshots,
    ICoinFlipService coinFlip,
    IClock clock)
{
    private const int MaxRetries = 3;

    public async Task<Result<GameData>> StartGameAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var game = await games.GetByIdAsync(gameId, cancellationToken);
        if (game is null)
            return Result<GameData>.Failure(new Error("Game.NotFound", "Game not found."));
        if (game.Status != GameStatus.InLobby)
            return Result<GameData>.Failure(new Error("Game.NotJoinable", "Game already started."));
        if (game.Players.Count != 2 || game.Players.Any(p => !p.IsReady || p.DeckId is null))
            return Result<GameData>.Failure(new Error("Game.NotReady", "Both players must be ready."));

        var deckCards = new List<List<Card>>();
        foreach (var player in game.Players.OrderBy(p => p.Order))
        {
            var entry = await decks.GetByIdAsync(player.DeckId!.Value, cancellationToken);
            if (entry is null || entry.OwnerId != player.UserId)
                return Result<GameData>.Failure(new Error("Deck.NotFound", "Deck not found."));
            var expanded = await ExpandDeckAsync(entry, cancellationToken);
            if (expanded is null)
                return Result<GameData>.Failure(new Error("Deck.Invalid", "Deck contains unknown cards."));
            deckCards.Add(expanded);
        }

        var startingPlayer = coinFlip.FlipHeads() ? 0 : 1;
        GameState state;
        try
        {
            state = engine.NewGame(deckCards[0], deckCards[1], startingPlayer, gameId: gameId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<GameData>.Failure(new Error("Deck.Invalid", ex.Message));
        }

        var now = clock.UtcNow;
        var updated = game with
        {
            Status = GameStatus.Setup,
            CurrentPlayerIndex = startingPlayer,
            TurnNumber = 1,
            Phase = TurnPhase.Draw,
            StartingPlayerIndex = startingPlayer,
            Snapshot = snapshots.Serialize(state),
            UpdatedAt = now,
        };

        try
        {
            await games.UpdateAsync(updated, game.Version, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<GameData>.Failure(new Error("Game.Conflict", "Game changed. Retry."));
        }

        await logs.AppendAsync(gameId, 1, null, "GameStart", "Setup", cancellationToken);
        return Result<GameData>.Success(updated);
    }

    public async Task<Result<GameActionResultDto>> ExecuteActionAsync(
        Guid gameId, Guid userId, GameActionRequest request, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            var game = await games.GetByIdAsync(gameId, cancellationToken);
            if (game is null)
                return Fail("Game.NotFound", "Game not found.");
            if (game.Status == GameStatus.Paused)
                return Fail("Game.Paused", "Game is paused.");
            if (game.Status is GameStatus.Finished or GameStatus.Cancelled)
                return Fail("Game.Finished", "Game is over.");

            var playerIndex = game.Players.FirstOrDefault(p => p.UserId == userId)?.Order;
            if (playerIndex is null)
                return Fail("Game.NotFound", "Game not found.");
            if (game.Snapshot is null)
                return Fail("Game.NotStarted", "Game has not started.");

            GameState state;
            try
            {
                state = await LoadStateAsync(game, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return Fail("Game.Corrupt", ex.Message);
            }

            GameAction action;
            try
            {
                action = BuildAction(state, playerIndex.Value, request);
            }
            catch (InvalidOperationException ex)
            {
                return Fail("Action.Invalid", ex.Message);
            }

            var result = engine.Execute(state, action);
            var updated = game with
            {
                Status = state.Status,
                CurrentPlayerIndex = state.CurrentPlayerIndex,
                TurnNumber = state.TurnNumber,
                Phase = state.Phase,
                WinnerId = state.WinnerPlayerIndex is null
                    ? null
                    : game.Players.First(p => p.Order == state.WinnerPlayerIndex.Value).UserId,
                Snapshot = snapshots.Serialize(state),
                UpdatedAt = clock.UtcNow,
            };

            try
            {
                await games.UpdateAsync(updated, game.Version, cancellationToken);
            }
            catch (ConcurrencyConflictException) when (attempt < MaxRetries - 1)
            {
                continue;
            }

            await logs.AppendAsync(gameId, state.TurnNumber, userId,
                request.Type.ToString(), result.Success ? "Ok" : $"Error:{result.Error}",
                cancellationToken);
            return Result<GameActionResultDto>.Success(new GameActionResultDto(
                result.Success, result.Error, result.PendingChoice, result.Events ?? []));
        }

        return Fail("Game.Conflict", "Game changed concurrently. Retry.");
    }

    public async Task<Result<bool>> PerformMulliganAsync(
        Guid gameId, Guid userId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var game = await games.GetByIdAsync(gameId, cancellationToken);
            if (game is null)
                return Result<bool>.Failure(new Error("Game.NotFound", "Game not found."));
            var playerIndex = game.Players.FirstOrDefault(p => p.UserId == userId)?.Order;
            if (playerIndex is null || game.Snapshot is null)
                return Result<bool>.Failure(new Error("Game.NotFound", "Game not found."));
            if (game.Status != GameStatus.Setup)
                return Result<bool>.Failure(new Error("Game.NotJoinable", "Not in setup."));

            var state = await LoadStateAsync(game, cancellationToken);
            if (!new SetupService().NeedsMulligan(state.Players[playerIndex.Value]))
                return Result<bool>.Failure(new Error("Game.NoMulligan", "Hand has a Basic Pokemon."));

            engine.PerformMulligan(state, playerIndex.Value);
            var updated = game with { Snapshot = snapshots.Serialize(state), UpdatedAt = clock.UtcNow };
            try
            {
                await games.UpdateAsync(updated, game.Version, cancellationToken);
            }
            catch (ConcurrencyConflictException) when (attempt == 0)
            {
                continue;
            }

            await logs.AppendAsync(gameId, 0, userId, "Mulligan", "Ok", cancellationToken);
            return Result<bool>.Success(true);
        }

        return Result<bool>.Failure(new Error("Game.Conflict", "Game changed. Retry."));
    }

    public async Task<Result<GameData>> PauseAsync(
        Guid gameId, Guid userId, CancellationToken cancellationToken)
    {
        var game = await games.GetByIdAsync(gameId, cancellationToken);
        if (game is null || game.Players.All(p => p.UserId != userId))
            return Result<GameData>.Failure(new Error("Game.NotFound", "Game not found."));
        if (game.Status is not (GameStatus.Setup or GameStatus.InProgress))
            return Result<GameData>.Failure(new Error("Game.NotJoinable", "Nothing to pause."));

        var updated = game with
        {
            PreviousStatus = (int)game.Status,
            Status = GameStatus.Paused,
            PausedByUserId = userId,
            PausedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow,
        };
        try
        {
            await games.UpdateAsync(updated, game.Version, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<GameData>.Failure(new Error("Game.Conflict", "Game changed. Retry."));
        }

        return Result<GameData>.Success(updated);
    }

    public async Task<Result<GameData>> ResumeAsync(
        Guid gameId, Guid userId, CancellationToken cancellationToken)
    {
        var game = await games.GetByIdAsync(gameId, cancellationToken);
        if (game is null || game.Players.All(p => p.UserId != userId))
            return Result<GameData>.Failure(new Error("Game.NotFound", "Game not found."));
        if (game.Status != GameStatus.Paused)
            return Result<GameData>.Failure(new Error("Game.NotJoinable", "Game is not paused."));

        var updated = game with
        {
            Status = game.PreviousStatus is null ? GameStatus.InProgress : (GameStatus)game.PreviousStatus,
            PreviousStatus = null,
            PausedByUserId = null,
            PausedAt = null,
            UpdatedAt = clock.UtcNow,
        };
        try
        {
            await games.UpdateAsync(updated, game.Version, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<GameData>.Failure(new Error("Game.Conflict", "Game changed. Retry."));
        }

        return Result<GameData>.Success(updated);
    }

    public async Task<Result<GameState>> LoadGameStateAsync(
        Guid gameId, Guid userId, CancellationToken cancellationToken)
    {
        var game = await games.GetByIdAsync(gameId, cancellationToken);
        if (game is null || game.Snapshot is null)
            return Result<GameState>.Failure(new Error("Game.NotFound", "Game not found."));
        if (game.Players.All(p => p.UserId != userId) && !game.AcceptsSpectators)
            return Result<GameState>.Failure(new Error("Game.NotFound", "Game not found."));

        try
        {
            return Result<GameState>.Success(await LoadStateAsync(game, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Result<GameState>.Failure(new Error("Game.Corrupt", ex.Message));
        }
    }

    private async Task<GameState> LoadStateAsync(GameData game, CancellationToken cancellationToken)
    {
        var ids = snapshots.CollectCardIds(game.Snapshot!);
        var cardsById = await cards.GetByIdsAsync(ids, cancellationToken);
        return snapshots.Deserialize(game.Snapshot!, cardsById);
    }

    private async Task<List<Card>?> ExpandDeckAsync(DeckEntry entry, CancellationToken cancellationToken)
    {
        var expanded = new List<Card>();
        foreach (var (cardId, quantity) in entry.Cards)
        {
            var card = await cards.GetByIdAsync(cardId, cancellationToken);
            if (card is null)
                return null;
            for (var i = 0; i < quantity; i++)
                expanded.Add(card);
        }

        return expanded;
    }

    private static GameAction BuildAction(GameState state, int playerIndex, GameActionRequest request)
    {
        var board = state.Players[playerIndex];
        Card? card = null;
        if (request.CardId is not null)
        {
            card = board.Hand.FirstOrDefault(c => c.Id == request.CardId)
                ?? throw new InvalidOperationException($"Card '{request.CardId}' not in hand.");
        }

        PokemonInPlay? target = null;
        if (request.Target is not null)
        {
            target = request.Target.Zone switch
            {
                "active" => board.Active,
                "bench" => request.Target.Index >= 0 && request.Target.Index < board.Bench.Count
                    ? board.Bench[request.Target.Index]
                    : null,
                _ => null,
            } ?? throw new InvalidOperationException("Target not in play.");
        }

        Attack? attack = null;
        if (request.AttackName is not null)
        {
            attack = board.Active?.Card.Attacks.FirstOrDefault(a => a.Name == request.AttackName)
                ?? throw new InvalidOperationException($"Attack '{request.AttackName}' unknown.");
        }

        var energies = request.EnergyIdsToDiscard?
            .Select(id => board.Active?.AttachedEnergies.FirstOrDefault(e => e.Id == id)
                ?? throw new InvalidOperationException($"Energy '{id}' not attached."))
            .ToList();

        return new GameAction(
            request.Type, playerIndex, card, target, attack, energies,
            request.ChoiceId, request.SelectedOptions, request.AbilityName);
    }

    private static Result<GameActionResultDto> Fail(string code, string message)
        => Result<GameActionResultDto>.Failure(new Error(code, message));
}
