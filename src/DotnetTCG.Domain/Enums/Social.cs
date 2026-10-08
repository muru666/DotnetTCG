namespace DotnetTCG.Domain.Enums;

public enum FriendshipStatus
{
    Pending,
    Accepted,
    Rejected,
    Blocked,
}

public enum GameEvent
{
    Knockout,
    PrizeTaken,
    SpecialConditionApplied,
    TurnStart,
    TurnEnd,
    GameOver,
    EnergyAttached,
    Evolution,
    Retreat,
    Mulligan,
    SetupComplete,
    GameStart,
    Attack,
}
