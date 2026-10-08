using System.Text.Json;
using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Infrastructure.Persistence.Snapshots;

public sealed class GameSnapshotMapper : IGameSnapshotSerializer
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string Serialize(GameState state)
        => JsonSerializer.Serialize(ToSnapshot(state), Json);

    public IReadOnlyList<string> CollectCardIds(string snapshot) => CollectIds(snapshot);

    private static IReadOnlyList<string> CollectIds(string snapshot)
    {
        var dto = JsonSerializer.Deserialize<GameStateSnapshot>(snapshot, Json)
            ?? throw new InvalidOperationException("Invalid game snapshot.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (dto.StadiumCardId is not null)
            ids.Add(dto.StadiumCardId);
        foreach (var board in dto.Players)
        {
            foreach (var id in board.Hand.Concat(board.Deck).Concat(board.Discard).Concat(board.Prizes))
                ids.Add(id);
            foreach (var pokemon in board.Active is null
                         ? board.Bench
                         : board.Bench.Append(board.Active))
            {
                ids.Add(pokemon.CardId);
                foreach (var id in pokemon.Energies)
                    ids.Add(id);
                if (pokemon.ToolId is not null)
                    ids.Add(pokemon.ToolId);
            }
        }

        return ids.ToList();
    }

    public GameState Deserialize(string snapshot, IReadOnlyDictionary<string, Card> cardsById)
    {
        var dto = JsonSerializer.Deserialize<GameStateSnapshot>(snapshot, Json)
            ?? throw new InvalidOperationException("Invalid game snapshot.");

        Card Resolve(string id) => cardsById.TryGetValue(id, out var card)
            ? card
            : throw new InvalidOperationException($"Card '{id}' missing from catalog.");

        var boards = dto.Players.Select(RestoreBoard).ToList();
        var state = new GameState(boards[0], boards[1], dto.StartingPlayerIndex, dto.SuddenDeath, dto.Id);
        state.ApplySnapshot(
            dto.Status, dto.Phase, dto.CurrentPlayerIndex, dto.TurnNumber,
            dto.StadiumCardId is null ? null : (TrainerCard)Resolve(dto.StadiumCardId),
            dto.PendingChoice, dto.PendingAttack, dto.WinnerPlayerIndex,
            dto.EnergiesAttached, dto.SupportersPlayed, dto.StadiumsPlayed,
            dto.Retreats, dto.AbilitiesUsed, dto.SupportersBlocked, dto.MulliganCounts);
        foreach (var (confirmed, i) in dto.SetupConfirmed.Select((c, i) => (c, i)))
        {
            if (confirmed)
                state.ConfirmSetup(i);
        }

        return state;

        PlayerBoard RestoreBoard(BoardSnapshot b)
        {
            var board = new PlayerBoard();
            board.RestoreState(
                b.Hand.Select(Resolve).ToList(),
                b.Deck.Select(Resolve).ToList(),
                b.Discard.Select(Resolve).ToList(),
                b.Prizes.Select(Resolve).ToList(),
                b.Active is null ? null : RestorePokemon(b.Active),
                b.Bench.Select(RestorePokemon).ToList(),
                b.Modifiers.Select(m => new BoardModifier(m.Type, m.Source, m.Value, m.Duration, m.TurnApplied)).ToList());
            return board;
        }

        PokemonInPlay RestorePokemon(InPlaySnapshot p)
        {
            var pokemon = new PokemonInPlay((PokemonCard)Resolve(p.CardId));
            pokemon.Restore(
                p.TotalDamage,
                p.Energies.Select(id => (EnergyCard)Resolve(id)).ToList(),
                p.ToolId is null ? null : (TrainerCard)Resolve(p.ToolId),
                p.Statuses,
                p.Modifiers.Select(m => new BoardModifier(m.Type, m.Source, m.Value, m.Duration, m.TurnApplied)).ToList(),
                p.JustPlayed,
                p.CannotAttackUntilTurn);
            return pokemon;
        }
    }

    private static GameStateSnapshot ToSnapshot(GameState state) => new(
        state.Id,
        state.Status,
        state.Phase,
        state.CurrentPlayerIndex,
        state.TurnNumber,
        state.StartingPlayerIndex,
        state.SuddenDeath,
        (int[])state.MulliganCounts.Clone(),
        (bool[])state.SetupConfirmed.Clone(),
        state.StadiumCard?.Id,
        state.PendingChoice,
        state.PendingAttack,
        state.WinnerPlayerIndex,
        state.EnergiesAttachedThisTurn,
        state.SupportersPlayedThisTurn,
        state.StadiumsPlayedThisTurn,
        state.RetreatsThisTurn,
        state.AbilitiesUsedThisTurn,
        (int[])state.SupportersBlockedUntilTurn.Clone(),
        state.Players.Select(ToBoardSnapshot).ToList());

    private static BoardSnapshot ToBoardSnapshot(PlayerBoard board) => new(
        board.Active is null ? null : ToPokemonSnapshot(board.Active),
        board.Bench.Select(ToPokemonSnapshot).ToList(),
        board.Hand.Select(c => c.Id).ToList(),
        board.DeckCards.Select(c => c.Id).ToList(),
        board.DiscardPile.Select(c => c.Id).ToList(),
        board.PrizeCards.Select(c => c.Id).ToList(),
        board.Modifiers.Select(m => new ModifierSnapshot(m.Type, m.Source, m.Value, m.Duration, m.TurnApplied)).ToList());

    private static InPlaySnapshot ToPokemonSnapshot(PokemonInPlay pokemon) => new(
        pokemon.Card.Id,
        pokemon.TotalDamage,
        pokemon.AttachedEnergies.Select(e => e.Id).ToList(),
        pokemon.AttachedTool?.Id,
        pokemon.Statuses.ToList(),
        pokemon.Modifiers.Select(m => new ModifierSnapshot(m.Type, m.Source, m.Value, m.Duration, m.TurnApplied)).ToList(),
        pokemon.JustPlayed,
        pokemon.CannotAttackUntilTurn);
}
