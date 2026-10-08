using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Common;
using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Domain.Game;

public sealed class TurnManager
{
    public void StartTurn(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.ResetTurnCounters();
        state.SetPhase(TurnPhase.Draw);
    }

    public void GoToMain(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Phase != TurnPhase.Draw)
            throw new InvalidOperationException("Can only go to MAIN from DRAW.");
        state.SetPhase(TurnPhase.Main);
    }

    public bool CanGoToAttack(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Phase != TurnPhase.Main)
            return false;
        if (state.IsFirstTurn && state.IsStartingPlayerTurn)
            return false;
        return true;
    }

    public void GoToAttack(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!CanGoToAttack(state))
            throw new InvalidOperationException("Cannot go to ATTACK in the current phase/turn.");
        state.SetPhase(TurnPhase.Attack);
    }

    public void EndTurn(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.SetPhase(TurnPhase.BetweenTurns);
        state.AdvanceTurn();
        state.SetPhase(TurnPhase.Draw);
    }

    public bool ShouldAutoDraw(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return !(state.IsFirstTurn && state.IsStartingPlayerTurn);
    }

    public Result<Card?> ProcessAutoDraw(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!ShouldAutoDraw(state))
        {
            state.SetPhase(TurnPhase.Main);
            return Result<Card?>.Success(null);
        }

        var drawn = state.CurrentPlayer.DrawCard();
        if (drawn.IsFailure)
            return Result<Card?>.Failure(drawn.Error);

        state.SetPhase(TurnPhase.Main);
        return Result<Card?>.Success(drawn.Value);
    }
}
