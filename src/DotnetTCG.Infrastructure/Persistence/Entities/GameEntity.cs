namespace DotnetTCG.Infrastructure.Persistence.Entities;

public sealed class GameEntity
{
    public Guid Id { get; set; }
    public int Status { get; set; }
    public int CurrentPlayerIndex { get; set; }
    public int TurnNumber { get; set; }
    public int TurnPhase { get; set; }
    public int StartingPlayerIndex { get; set; }
    public bool SuddenDeath { get; set; }
    public Guid? WinnerId { get; set; }
    public Guid CreatorId { get; set; }
    public int? PreviousStatus { get; set; }
    public Guid? PausedByUserId { get; set; }
    public DateTimeOffset? PausedAt { get; set; }    public UserEntity? Creator { get; set; }
    public bool IsPublic { get; set; } = true;
    public bool AcceptsSpectators { get; set; } = true;
    public string? PasswordHash { get; set; }
    public string? StateSnapshot { get; set; }
    public uint Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public List<GamePlayerEntity> Players { get; set; } = [];
}

public sealed class GamePlayerEntity
{
    public Guid GameId { get; set; }
    public GameEntity? Game { get; set; }
    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }
    public int PlayerOrder { get; set; }
    public Guid? DeckId { get; set; }
    public DeckEntity? Deck { get; set; }
    public bool IsReady { get; set; }
}

public sealed class GameLogEntity
{
    public long Id { get; set; }
    public Guid GameId { get; set; }
    public GameEntity? Game { get; set; }
    public int TurnNumber { get; set; }
    public Guid? ActingUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
