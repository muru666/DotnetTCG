using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Engine.Handlers;

public sealed class TurnControlHandler(EngineServices services, KnockoutResolver knockouts)
{
    public string? PassTurn(GameState state, int playerIndex, List<GameEvent> events)
    {
        if (state.CurrentPlayerIndex != playerIndex)
            return "Turn.NotYourTurn";
        if (state.Phase == TurnPhase.Draw)
        {
            // El robo inicial es obligatorio: o se roba acá, o se pierde por
            // mazo vacío (antes el PassTurn esquivaba el robo y la derrota).
            var draw = services.Turns.ProcessAutoDraw(state);
            if (draw.IsFailure)
            {
                state.Finish(1 - playerIndex);
                events.Add(GameEvent.GameOver);
                return "Game.DeckOut";
            }
        }
        EndTurn(state, events);
        return null;
    }

    public string? Concede(GameState state, int playerIndex)
    {
        state.Finish(1 - playerIndex);
        return null;
    }

    public void EndTurn(GameState state, List<GameEvent> events)
    {
        foreach (var board in state.Players)
        {
            foreach (var pokemon in FieldOf(board))
                services.Statuses.ProcessBetweenTurns(pokemon, services.CoinFlip);
        }

        knockouts.Resolve(state, events);
        events.Add(GameEvent.TurnEnd);

        services.Turns.EndTurn(state);

        foreach (var board in state.Players)
        {
            board.RemoveExpiredModifiers(state.TurnNumber);
            foreach (var pokemon in FieldOf(board))
            {
                pokemon.RemoveExpiredModifiers(state.TurnNumber);
                pokemon.JustPlayed = false;
            }
        }

        var draw = services.Turns.ProcessAutoDraw(state);
        if (draw.IsFailure)
        {
            state.Finish(1 - state.CurrentPlayerIndex);
            events.Add(GameEvent.GameOver);
            return;
        }

        events.Add(GameEvent.TurnStart);
    }

    private static List<PokemonInPlay> FieldOf(PlayerBoard board)
    {
        var field = new List<PokemonInPlay>();
        if (board.Active is not null)
            field.Add(board.Active);
        field.AddRange(board.Bench);
        return field;
    }
}
