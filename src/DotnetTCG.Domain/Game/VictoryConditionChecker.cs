namespace DotnetTCG.Domain.Game;

public sealed record VictoryResult(bool GameOver, int? WinnerPlayerIndex, bool RequiresSuddenDeath);

public sealed class VictoryConditionChecker
{
    public VictoryResult Check(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var prizes0Empty = state.Players[0].PrizeCount == 0;
        var prizes1Empty = state.Players[1].PrizeCount == 0;
        if (prizes0Empty && prizes1Empty)
            return new VictoryResult(GameOver: false, WinnerPlayerIndex: null, RequiresSuddenDeath: true);
        if (prizes0Empty)
            return new VictoryResult(GameOver: true, WinnerPlayerIndex: 0, RequiresSuddenDeath: false);
        if (prizes1Empty)
            return new VictoryResult(GameOver: true, WinnerPlayerIndex: 1, RequiresSuddenDeath: false);

        var deck0Empty = state.Players[0].DeckCount == 0;
        var deck1Empty = state.Players[1].DeckCount == 0;
        if (deck0Empty && deck1Empty)
            return new VictoryResult(GameOver: false, WinnerPlayerIndex: null, RequiresSuddenDeath: true);
        if (deck0Empty)
            return new VictoryResult(GameOver: true, WinnerPlayerIndex: 1, RequiresSuddenDeath: false);
        if (deck1Empty)
            return new VictoryResult(GameOver: true, WinnerPlayerIndex: 0, RequiresSuddenDeath: false);

        var field0Empty = state.Players[0].Active is null && state.Players[0].Bench.Count == 0;
        var field1Empty = state.Players[1].Active is null && state.Players[1].Bench.Count == 0;
        if (field0Empty && field1Empty)
            return new VictoryResult(GameOver: false, WinnerPlayerIndex: null, RequiresSuddenDeath: true);
        if (field0Empty)
            return new VictoryResult(GameOver: true, WinnerPlayerIndex: 1, RequiresSuddenDeath: false);
        if (field1Empty)
            return new VictoryResult(GameOver: true, WinnerPlayerIndex: 0, RequiresSuddenDeath: false);

        return new VictoryResult(GameOver: false, WinnerPlayerIndex: null, RequiresSuddenDeath: false);
    }
}
