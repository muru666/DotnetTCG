using DotnetTCG.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DotnetTCG.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<CardEntity> Cards => Set<CardEntity>();
    public DbSet<DeckEntity> Decks => Set<DeckEntity>();
    public DbSet<DeckCardEntity> DeckCards => Set<DeckCardEntity>();
    public DbSet<GameEntity> Games => Set<GameEntity>();
    public DbSet<GamePlayerEntity> GamePlayers => Set<GamePlayerEntity>();
    public DbSet<GameLogEntity> GameLogs => Set<GameLogEntity>();
    public DbSet<RecoveryTokenEntity> RecoveryTokens => Set<RecoveryTokenEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
