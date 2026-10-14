using DotnetTCG.Application.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Application.Games;

public sealed record FieldPokemonDto(
    string CardId,
    string Name,
    int Hp,
    int HpRemaining,
    int DamageCounters,
    IReadOnlyList<string> Energies,
    string? Tool,
    IReadOnlyList<Status> Statuses,
    int TotalEnergy);

public sealed record BoardViewDto(
    FieldPokemonDto? Active,
    IReadOnlyList<FieldPokemonDto> Bench,
    int DeckCount,
    int PrizeCount,
    IReadOnlyList<string> Discard,
    IReadOnlyList<CardDto>? Hand,
    int HandCount);

public sealed record GameStateViewDto(
    Guid GameId,
    GameStatus Status,
    TurnPhase Phase,
    int TurnNumber,
    int CurrentPlayerIndex,
    int? ViewerPlayerIndex,
    IReadOnlyList<BoardViewDto> Boards,
    string? StadiumCardId,
    PendingChoice? PendingChoice,
    int? WinnerPlayerIndex,
    bool SuddenDeath,
    int StartingPlayerIndex);

public static class StateProjector
{
    public static GameStateViewDto Project(GameState state, int? viewerOrder)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new GameStateViewDto(
            state.Id,
            state.Status,
            state.Phase,
            state.TurnNumber,
            state.CurrentPlayerIndex,
            viewerOrder,
            [ProjectBoard(state.Players[0], viewerOrder == 0), ProjectBoard(state.Players[1], viewerOrder == 1)],
            state.StadiumCard?.Id,
            state.PendingChoice is null || state.PendingChoice.PlayerIndex != viewerOrder
                ? null
                : state.PendingChoice,
            state.WinnerPlayerIndex,
            state.SuddenDeath,
            state.StartingPlayerIndex);
    }

    private static BoardViewDto ProjectBoard(PlayerBoard board, bool isOwner)
        => new(
            board.Active is null ? null : ProjectPokemon(board.Active),
            board.Bench.Select(ProjectPokemon).ToList(),
            board.DeckCount,
            board.PrizeCount,
            board.DiscardPile.Select(c => c.Id).ToList(),
            isOwner ? board.Hand.Select(CardDto.FromDomain).ToList() : null,
            board.Hand.Count);

    private static FieldPokemonDto ProjectPokemon(PokemonInPlay pokemon) => new(
        pokemon.Card.Id,
        pokemon.Card.Name,
        pokemon.Card.Hp,
        pokemon.HpRemaining,
        pokemon.DamageCounters,
        pokemon.AttachedEnergies.Select(e => e.Id).ToList(),
        pokemon.AttachedTool?.Id,
        pokemon.Statuses.ToList(),
        pokemon.TotalEnergyCount);
}
