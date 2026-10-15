using DotnetTCG.Api.Hubs;
using DotnetTCG.Api.Infrastructure;
using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Application.Games;
using DotnetTCG.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotnetTCG.Api.Controllers;

public sealed record CreateGameRequest(bool IsPublic = true, bool AcceptsSpectators = true, string? Password = null);
public sealed record JoinGameRequest(string? Password = null);
public sealed record SelectDeckRequest(Guid DeckId);
public sealed record ReadyRequest(bool Ready);
public sealed record GameSettingsRequest(bool IsPublic, bool AcceptsSpectators, string? Password);
public sealed record RemovePlayerRequest(Guid TargetUserId);
public sealed record GameActionHttpRequest(
    string Type,
    string? CardId = null,
    GameActionTarget? Target = null,
    string? AttackName = null,
    IReadOnlyList<string>? EnergyIdsToDiscard = null,
    string? ChoiceId = null,
    IReadOnlyList<string>? SelectedOptions = null,
    string? AbilityName = null);
public sealed record GameActionTarget(string Zone, int Index);

[ApiController]
[Route("v1/games")]
[Authorize]
public sealed class GamesController(
    LobbyService lobby,
    GameExecutionService execution,
    IGameLogRepository gameLogs,
    GameConnectionTracker tracker,
    TimeProvider clock) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<LobbyDto>> Create([FromBody] CreateGameRequest request, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        var result = await lobby.CreateAsync(
            userId.Value, request.IsPublic, request.AcceptsSpectators, request.Password, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetLobby), new { id = result.Value!.Id }, result.Value)
            : this.ToActionResult(result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LobbyDto>>> ListOpen(CancellationToken ct)
    {
        // Mantenimiento oportunista: los lobbies abandonados se borran solos.
        await lobby.CleanupStaleAsync(clock.GetUtcNow().AddHours(-24), ct);
        var open = await lobby.ListOpenAsync(ct);
        // Un lobby solo existe mientras su creador sigue conectado.
        return Ok(open.Where(l => tracker.IsConnected(l.CreatorId)).ToList());
    }

    [HttpGet("{id:guid}/lobby")]
    public async Task<ActionResult<LobbyDto>> GetLobby(Guid id, CancellationToken ct)
        => this.ToActionResult(await lobby.GetAsync(id, ct));

    [HttpPost("{id:guid}/join")]
    public async Task<ActionResult<LobbyDto>> Join(
        Guid id, [FromBody] JoinGameRequest request, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        return this.ToActionResult(await lobby.JoinAsync(id, userId.Value, request.Password, ct));
    }

    [HttpPost("{id:guid}/deck")]
    public async Task<ActionResult<LobbyDto>> SelectDeck(
        Guid id, [FromBody] SelectDeckRequest request, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        return this.ToActionResult(await lobby.SelectDeckAsync(id, userId.Value, request.DeckId, ct));
    }

    [HttpPost("{id:guid}/ready")]
    public async Task<ActionResult<LobbyDto>> SetReady(
        Guid id, [FromBody] ReadyRequest request, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        return this.ToActionResult(await lobby.SetReadyAsync(id, userId.Value, request.Ready, ct));
    }

    [HttpPost("{id:guid}/remove-player")]
    public async Task<ActionResult<LobbyDto>> RemovePlayer(
        Guid id, [FromBody] RemovePlayerRequest request, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        return this.ToActionResult(
            await lobby.RemovePlayerAsync(id, userId.Value, request.TargetUserId, ct));
    }

    [HttpPost("{id:guid}/leave")]
    public async Task<ActionResult> Leave(Guid id, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        var result = await lobby.LeaveAsync(id, userId.Value, ct);
        return result.IsSuccess
            ? Ok(new { deleted = result.Value!.Deleted, lobby = result.Value.Lobby })
            : this.ToActionResult(result);
    }

    [HttpPut("{id:guid}/settings")]
    public async Task<ActionResult<LobbyDto>> UpdateSettings(
        Guid id, [FromBody] GameSettingsRequest request, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        return this.ToActionResult(await lobby.UpdateSettingsAsync(
            id, userId.Value, request.IsPublic, request.AcceptsSpectators, request.Password, ct));
    }

    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<GameSummaryDto>> Start(Guid id, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        var result = await execution.StartGameAsync(id, ct);
        return result.IsSuccess
            ? Ok(ToSummary(result.Value!))
            : this.ToActionResult(result);
    }

    [HttpGet("my-resumable")]
    public async Task<ActionResult<IReadOnlyList<GameSummaryDto>>> MyResumable(CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        var games = await execution.ListResumableAsync(userId.Value, ct);
        return Ok(games.Select(ToSummary).ToList());
    }

    [HttpGet("{id:guid}/state")]
    public async Task<ActionResult<GameStateViewDto>> GetState(Guid id, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        var result = await execution.LoadGameStateAsync(id, userId.Value, ct);
        if (result.IsFailure)
            return this.ToActionResult(result);
        var state = result.Value!;
        var order = await PlayerOrderAsync(id, userId.Value, ct);
        return Ok(StateProjector.Project(state, order));
    }

    [HttpPost("{id:guid}/actions")]
    public async Task<ActionResult<GameActionResultDto>> Act(
        Guid id, [FromBody] GameActionHttpRequest request, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        if (!Enum.TryParse<GameActionType>(request.Type, ignoreCase: true, out var type))
            return BadRequest(new { message = $"Unknown action '{request.Type}'." });
        var result = await execution.ExecuteActionAsync(id, userId.Value, new GameActionRequest(
            type, request.CardId,
            request.Target is null ? null : new TargetRef(request.Target.Zone, request.Target.Index),
            request.AttackName, request.EnergyIdsToDiscard, request.ChoiceId,
            request.SelectedOptions, request.AbilityName), ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToActionResult(result);
    }

    [HttpPost("{id:guid}/mulligan")]
    public async Task<ActionResult> Mulligan(Guid id, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        var result = await execution.PerformMulliganAsync(id, userId.Value, ct);
        return result.IsSuccess ? Ok(new { ok = true }) : this.ToActionResult(result);
    }

    [HttpPost("{id:guid}/pause")]
    public async Task<ActionResult<GameSummaryDto>> Pause(Guid id, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        var result = await execution.PauseAsync(id, userId.Value, ct);
        return result.IsSuccess ? Ok(ToSummary(result.Value!)) : this.ToActionResult(result);
    }

    [HttpPost("{id:guid}/resume")]
    public async Task<ActionResult<GameSummaryDto>> Resume(Guid id, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        var result = await execution.ResumeAsync(id, userId.Value, ct);
        return result.IsSuccess ? Ok(ToSummary(result.Value!)) : this.ToActionResult(result);
    }

    [HttpGet("{id:guid}/logs")]
    public async Task<ActionResult<IReadOnlyList<GameLogEntry>>> Logs(Guid id, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        var state = await execution.LoadGameStateAsync(id, userId.Value, ct);
        if (state.IsFailure)
            return this.ToActionResult(state);
        return Ok(await gameLogs.ListByGameAsync(id, ct));
    }

    private async Task<int?> PlayerOrderAsync(Guid gameId, Guid userId, CancellationToken ct)
    {
        var lobbyState = await lobby.GetAsync(gameId, ct);
        return lobbyState.IsSuccess
            ? lobbyState.Value!.Players.FirstOrDefault(p => p.UserId == userId)?.Order
            : null;
    }

    private static GameSummaryDto ToSummary(GameData game) => new(
        game.Id, game.Status, game.TurnNumber, game.Phase, game.CurrentPlayerIndex, game.WinnerId,
        game.Players.Select(p => new LobbyPlayerDto(p.UserId, p.Order, p.DeckId, p.IsReady, p.Username)).ToList());
}
