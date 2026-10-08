using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Domain.Game;

public sealed class GameState
{
    public GameState(PlayerBoard player0, PlayerBoard player1, int startingPlayerIndex, bool suddenDeath)
    {
        ArgumentNullException.ThrowIfNull(player0);
        ArgumentNullException.ThrowIfNull(player1);
        if (startingPlayerIndex is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(startingPlayerIndex));

        Id = Guid.NewGuid();
        Players = [player0, player1];
        StartingPlayerIndex = startingPlayerIndex;
        CurrentPlayerIndex = startingPlayerIndex;
        SuddenDeath = suddenDeath;
        Status = GameStatus.Setup;
        Phase = TurnPhase.Draw;
        TurnNumber = 1;
    }

    public Guid Id { get; }
    public PlayerBoard[] Players { get; }
    public GameStatus Status { get; private set; }
    public TurnPhase Phase { get; private set; }
    public int CurrentPlayerIndex { get; private set; }
    public int TurnNumber { get; private set; }
    public int StartingPlayerIndex { get; }
    public bool SuddenDeath { get; }
    public int[] MulliganCounts { get; } = [0, 0];
    public TrainerCard? StadiumCard { get; private set; }
    public PendingChoice? PendingChoice { get; private set; }
    public int? WinnerPlayerIndex { get; private set; }

    public int EnergiesAttachedThisTurn { get; private set; }
    public int SupportersPlayedThisTurn { get; private set; }
    public int StadiumsPlayedThisTurn { get; private set; }
    public int RetreatsThisTurn { get; private set; }
    public int AbilitiesUsedThisTurn { get; private set; }
    public int[] SupportersBlockedUntilTurn { get; } = [-1, -1];

    public bool IsFirstTurn => TurnNumber == 1;
    public bool IsStartingPlayerTurn => CurrentPlayerIndex == StartingPlayerIndex;

    public PlayerBoard CurrentPlayer => Players[CurrentPlayerIndex];
    public PlayerBoard OpponentOf(int playerIndex) => Players[1 - playerIndex];

    public void SetStatus(GameStatus status) => Status = status;
    public void SetPhase(TurnPhase phase) => Phase = phase;

    public void AdvanceTurn()
    {
        CurrentPlayerIndex = 1 - CurrentPlayerIndex;
        TurnNumber++;
        ResetTurnCounters();
    }

    public void ResetTurnCounters()
    {
        EnergiesAttachedThisTurn = 0;
        SupportersPlayedThisTurn = 0;
        StadiumsPlayedThisTurn = 0;
        RetreatsThisTurn = 0;
        AbilitiesUsedThisTurn = 0;
    }

    public void RegisterEnergyAttached() => EnergiesAttachedThisTurn++;
    public void RegisterSupporterPlayed() => SupportersPlayedThisTurn++;
    public void RegisterStadiumPlayed() => StadiumsPlayedThisTurn++;
    public void RegisterRetreat() => RetreatsThisTurn++;
    public void RegisterAbilityUsed() => AbilitiesUsedThisTurn++;

    public void SetStadium(TrainerCard stadium) => StadiumCard = stadium;

    public void SetPendingChoice(PendingChoice? choice) => PendingChoice = choice;

    public void RegisterMulligan(int playerIndex) => MulliganCounts[playerIndex]++;

    public void Finish(int? winnerPlayerIndex)
    {
        Status = GameStatus.Finished;
        WinnerPlayerIndex = winnerPlayerIndex;
    }
}
