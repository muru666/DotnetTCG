namespace DotnetTCG.Domain.Enums;

public enum GameActionType
{
    PlayCard,
    Attack,
    Retreat,
    UseAbility,
    PassTurn,
    DrawCard,
    Evolve,
    AttachEnergy,
    PlaySupporter,
    PlayStadium,
    ActivateAbility,
}

public enum GameStatus
{
    InLobby,
    Setup,
    InProgress,
    Finished,
    Cancelled,
    Paused,
    Resuming,
}

public enum TurnPhase
{
    Draw,
    Main,
    Attack,
    BetweenTurns,
}

public enum Zone
{
    Hand,
    DeckPile,
    DiscardPile,
    PrizeCards,
}
