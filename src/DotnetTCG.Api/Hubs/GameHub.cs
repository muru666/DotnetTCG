using DotnetTCG.Api.Controllers;
using DotnetTCG.Api.Infrastructure;
using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Application.Games;
using DotnetTCG.Domain.Common;
using DotnetTCG.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DotnetTCG.Api.Hubs;

[Authorize]
public sealed class GameHub(
    LobbyService lobby,
    GameExecutionService execution,
    GameConnectionTracker tracker,
    TimeProvider time,
    IClock clock,
    IServiceScopeFactory scopes,
    ILogger<GameHub> logger) : Hub
{
    public static readonly TimeSpan DisconnectGracePeriod = TimeSpan.FromSeconds(15);
    public const int MaxChatLength = 500;

    private static string Group(Guid gameId) => $"game:{gameId}";

    private Guid? UserId => CurrentUser.Id(Context.User!);

    public async Task JoinLobby(Guid gameId, string? password)
    {
        var userId = RequireUser();
        if (userId is null)
            return;
        var result = await lobby.JoinAsync(gameId, userId.Value, password, Context.ConnectionAborted);
        if (result.IsFailure)
        {
            await SendError(result.Error);
            return;
        }

        tracker.Add(Context.ConnectionId, userId.Value, gameId, spectator: false);
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(gameId));
        await Clients.Group(Group(gameId)).SendAsync("lobby", result.Value);
    }

    public async Task LeaveLobby(Guid gameId)
    {
        var userId = RequireUser();
        if (userId is null)
            return;
        var result = await lobby.LeaveAsync(gameId, userId.Value, Context.ConnectionAborted);
        if (result.IsFailure)
        {
            await SendError(result.Error);
            return;
        }

        tracker.Remove(Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(gameId));
        if (!result.Value!.Deleted)
            await Clients.Group(Group(gameId)).SendAsync("lobby", result.Value.Lobby);
    }

    public async Task SelectDeck(Guid gameId, Guid deckId)
    {
        var userId = RequireUser();
        if (userId is null)
            return;
        var result = await lobby.SelectDeckAsync(gameId, userId.Value, deckId, Context.ConnectionAborted);
        if (result.IsFailure)
        {
            await SendError(result.Error);
            return;
        }

        tracker.Add(Context.ConnectionId, userId.Value, gameId, spectator: false);
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(gameId));
        await Clients.Group(Group(gameId)).SendAsync("lobby", result.Value);
    }

    public async Task SetReady(Guid gameId, bool ready)
    {
        var userId = RequireUser();
        if (userId is null)
            return;
        var result = await lobby.SetReadyAsync(gameId, userId.Value, ready, Context.ConnectionAborted);
        if (result.IsFailure)
        {
            await SendError(result.Error);
            return;
        }

        await Clients.Group(Group(gameId)).SendAsync("lobby", result.Value);

        var updated = result.Value!;
        if (ready && updated.Players.Count == 2 && updated.Players.All(p => p.IsReady))
        {
            await StartGame(gameId);
        }
    }

    public async Task StartGame(Guid gameId)
    {
        var userId = RequireUser();
        if (userId is null)
            return;
        var result = await execution.StartGameAsync(gameId, Context.ConnectionAborted);
        if (result.IsFailure)
        {
            await SendError(result.Error);
            return;
        }

        await PushStatesAsync(gameId);
        await Clients.Group(Group(gameId)).SendAsync("game-started", new { gameId });
    }

    public async Task ExecuteAction(Guid gameId, GameActionHttpRequest request)
    {
        var userId = RequireUser();
        if (userId is null)
            return;
        if (!Enum.TryParse<GameActionType>(request.Type, ignoreCase: true, out var type))
        {
            await SendError(new Error("Action.Invalid", $"Unknown action '{request.Type}'."));
            return;
        }

        var result = await execution.ExecuteActionAsync(gameId, userId.Value, new GameActionRequest(
            type, request.CardId,
            request.Target is null ? null : new TargetRef(request.Target.Zone, request.Target.Index),
            request.AttackName, request.EnergyIdsToDiscard, request.ChoiceId,
            request.SelectedOptions, request.AbilityName), Context.ConnectionAborted);
        if (result.IsFailure)
        {
            await SendError(result.Error);
            return;
        }

        var action = result.Value!;
        var actorConnections = tracker.ConnectionIdsOf(userId.Value, gameId).ToArray();
        await Clients.Clients(actorConnections).SendAsync("action-result", action);
        await Clients.GroupExcept(Group(gameId), actorConnections)
            .SendAsync("action-result", action with { PendingChoice = null });
        await PushStatesAsync(gameId);
    }

    public async Task Mulligan(Guid gameId)
    {
        var userId = RequireUser();
        if (userId is null)
            return;
        var result = await execution.PerformMulliganAsync(gameId, userId.Value, Context.ConnectionAborted);
        if (result.IsFailure)
        {
            await SendError(result.Error);
            return;
        }

        await PushStatesAsync(gameId);
    }

    public async Task PauseGame(Guid gameId)
    {
        var userId = RequireUser();
        if (userId is null)
            return;
        var result = await execution.PauseAsync(gameId, userId.Value, Context.ConnectionAborted);
        if (result.IsFailure)
        {
            await SendError(result.Error);
            return;
        }

        // La pausa vive en la fila del juego, no en el snapshot: avisar explícito.
        await Clients.Group(Group(gameId)).SendAsync("game-paused", new { gameId });
        await PushStatesAsync(gameId);
    }

    public async Task Reconnect(Guid gameId)
    {
        var userId = RequireUser();
        if (userId is null)
            return;
        var state = await execution.LoadGameStateAsync(gameId, userId.Value, Context.ConnectionAborted);
        if (state.IsFailure)
        {
            await SendError(state.Error);
            return;
        }

        var lobbyState = await lobby.GetAsync(gameId, Context.ConnectionAborted);
        var spectator = lobbyState.IsSuccess
            && lobbyState.Value!.Players.All(p => p.UserId != userId.Value);
        tracker.Add(Context.ConnectionId, userId.Value, gameId, spectator);
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(gameId));

        var resumed = await execution.ResumeAsync(gameId, userId.Value, Context.ConnectionAborted);
        if (resumed.IsFailure && resumed.Error.Code != "Game.NotJoinable")
        {
            await SendError(resumed.Error);
            return;
        }

        await PushStatesAsync(gameId);
    }

    public async Task JoinAsSpectator(Guid gameId)
    {
        var userId = RequireUser();
        if (userId is null)
            return;
        var state = await execution.LoadGameStateAsync(gameId, userId.Value, Context.ConnectionAborted);
        if (state.IsFailure)
        {
            await SendError(state.Error);
            return;
        }

        tracker.Add(Context.ConnectionId, userId.Value, gameId, spectator: true);
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(gameId));
        await Clients.Caller.SendAsync(
            "game-state", StateProjector.Project(state.Value!, viewerOrder: null));
    }

    public async Task SendChat(Guid gameId, string text)
    {
        var userId = RequireUser();
        if (userId is null)
            return;
        text = text.Trim();
        if (text.Length == 0 || text.Length > MaxChatLength)
        {
            await SendError(new Error("Chat.Invalid", "Message must be 1-500 chars."));
            return;
        }

        var state = await execution.LoadGameStateAsync(gameId, userId.Value, Context.ConnectionAborted);
        if (state.IsFailure)
        {
            await SendError(state.Error);
            return;
        }

        await Clients.Group(Group(gameId)).SendAsync("chat",
            new { senderId = userId.Value, text, at = clock.UtcNow });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tracked = tracker.Remove(Context.ConnectionId);
        if (tracked is not null && !tracked.Spectator && Context.UserIdentifier is not null
            && Guid.TryParse(Context.UserIdentifier, out var userId))
        {
            _ = PauseAfterGraceAsync(userId, tracked.GameId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task PauseAfterGraceAsync(Guid userId, Guid gameId)
    {
        try
        {
            // Sin token de conexión: el chequeo de HasConnection tras el delay
            // cubre la reconexión (el token de la conexión muerta ya no sirve).
            await Task.Delay(DisconnectGracePeriod, time);
            if (tracker.HasConnection(userId, gameId))
                return;

            // Scope propio: este Task sobrevive al scope de la invocación del hub,
            // así que no puede usar los servicios scoped inyectados en el hub.
            await using var scope = scopes.CreateAsyncScope();
            var scopedExecution = scope.ServiceProvider.GetRequiredService<GameExecutionService>();
            var scopedLobby = scope.ServiceProvider.GetRequiredService<LobbyService>();
            var paused = await scopedExecution.PauseAsync(gameId, userId, CancellationToken.None);
            if (paused.IsSuccess)
                await PushStatesAsync(scopedLobby, scopedExecution, gameId, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Disconnect pause failed for game {GameId}", gameId);
        }
    }

    private Task PushStatesAsync(Guid gameId)
        => PushStatesAsync(lobby, execution, gameId, Context.ConnectionAborted);

    private async Task PushStatesAsync(
        LobbyService lobbyService, GameExecutionService executionService, Guid gameId, CancellationToken ct)
    {
        var lobbyState = await lobbyService.GetAsync(gameId, ct);
        if (lobbyState.IsFailure)
            return;
        foreach (var player in lobbyState.Value!.Players)
        {
            var state = await executionService.LoadGameStateAsync(gameId, player.UserId, ct);
            if (state.IsFailure)
                continue;
            await Clients.User(player.UserId.ToString()).SendAsync(
                "game-state", StateProjector.Project(state.Value!, player.Order));
        }

        foreach (var spectator in tracker.SpectatorsOf(gameId))
        {
            var state = await executionService.LoadGameStateAsync(gameId, spectator, ct);
            if (state.IsFailure)
                continue;
            await Clients.User(spectator.ToString()).SendAsync(
                "game-state", StateProjector.Project(state.Value!, viewerOrder: null));
        }
    }

    private Guid? RequireUser()
    {
        var userId = UserId;
        if (userId is null)
            logger.LogWarning("Hub call without subject claim.");
        return userId;
    }

    private Task SendError(Error error)
        => Clients.Caller.SendAsync("error", new { code = error.Code, message = error.Message });
}
