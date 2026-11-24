using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Domain.Game;

public sealed class GameState
{
    public GameState(PlayerBoard player0, PlayerBoard player1, int startingPlayerIndex, bool suddenDeath, Guid? id = null)
    {
        ArgumentNullException.ThrowIfNull(player0);
        ArgumentNullException.ThrowIfNull(player1);
        if (startingPlayerIndex is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(startingPlayerIndex));

        Id = id ?? Guid.NewGuid();
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
    public bool SuddenDeath { get; private set; }
    public int[] MulliganCounts { get; } = [0, 0];
    public bool[] SetupConfirmed { get; } = [false, false];
    public TrainerCard? StadiumCard { get; private set; }
    public PendingChoice? PendingChoice { get; private set; }
    public PendingAttack? PendingAttack { get; private set; }
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
    public void SetCurrentPlayer(int playerIndex)
    {
        if (playerIndex is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(playerIndex));
        CurrentPlayerIndex = playerIndex;
    }

    public void ConfirmSetup(int playerIndex) => SetupConfirmed[playerIndex] = true;
    public bool AllSetupConfirmed => SetupConfirmed[0] && SetupConfirmed[1];

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

    /// <summary>Reinicio de turno (muerte súbita: la nueva partida arranca en 1).</summary>
    public void SetTurnNumber(int turnNumber) => TurnNumber = turnNumber;

    public void SetPendingChoice(PendingChoice? choice) => PendingChoice = choice;

    public void SetPendingAttack(PendingAttack? attack) => PendingAttack = attack;

    public void MarkSuddenDeath() => SuddenDeath = true;

    public void ResetSetup()
    {
        SetupConfirmed[0] = false;
        SetupConfirmed[1] = false;
    }

    public void ApplySnapshot(
        GameStatus status,
        TurnPhase phase,
        int currentPlayerIndex,
        int turnNumber,
        TrainerCard? stadiumCard,
        PendingChoice? pendingChoice,
        PendingAttack? pendingAttack,
        int? winnerPlayerIndex,
        int energiesAttached,
        int supportersPlayed,
        int stadiumsPlayed,
        int retreats,
        int abilitiesUsed,
        int[] supportersBlockedUntilTurn,
        int[] mulliganCounts)
    {
        Status = status;
        Phase = phase;
        CurrentPlayerIndex = currentPlayerIndex;
        TurnNumber = turnNumber;
        StadiumCard = stadiumCard;
        PendingChoice = pendingChoice;
        PendingAttack = pendingAttack;
        WinnerPlayerIndex = winnerPlayerIndex;
        EnergiesAttachedThisTurn = energiesAttached;
        SupportersPlayedThisTurn = supportersPlayed;
        StadiumsPlayedThisTurn = stadiumsPlayed;
        RetreatsThisTurn = retreats;
        AbilitiesUsedThisTurn = abilitiesUsed;
        SupportersBlockedUntilTurn[0] = supportersBlockedUntilTurn[0];
        SupportersBlockedUntilTurn[1] = supportersBlockedUntilTurn[1];
        MulliganCounts[0] = mulliganCounts[0];
        MulliganCounts[1] = mulliganCounts[1];
    }

    public void RegisterMulligan(int playerIndex) => MulliganCounts[playerIndex]++;

    public void BlockSupporters(int playerIndex, int untilTurn) =>
        SupportersBlockedUntilTurn[playerIndex] = untilTurn;

    public void Finish(int? winnerPlayerIndex)
    {
        Status = GameStatus.Finished;
        WinnerPlayerIndex = winnerPlayerIndex;
    }
}
