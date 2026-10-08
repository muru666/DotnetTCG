using DotnetTCG.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotnetTCG.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Username).IsRequired().HasMaxLength(50);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(200);
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Role).IsRequired().HasMaxLength(20);
        builder.HasIndex(u => u.Username).IsUnique();
        builder.HasIndex(u => u.Email).IsUnique();
    }
}

public sealed class CardConfiguration : IEntityTypeConfiguration<CardEntity>
{
    public void Configure(EntityTypeBuilder<CardEntity> builder)
    {
        builder.ToTable("cards");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasMaxLength(20);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Subtypes).HasMaxLength(200);
        builder.Property(c => c.SetId).IsRequired().HasMaxLength(20);
        builder.Property(c => c.ImageUrl).HasMaxLength(500);
        builder.Property(c => c.AttacksJson).HasColumnType("jsonb");
        builder.Property(c => c.AbilitiesJson).HasColumnType("jsonb");
        builder.Property(c => c.WeaknessesJson).HasColumnType("jsonb");
        builder.Property(c => c.ResistancesJson).HasColumnType("jsonb");
        builder.HasIndex(c => c.Name);
    }
}

public sealed class DeckConfiguration : IEntityTypeConfiguration<DeckEntity>
{
    public void Configure(EntityTypeBuilder<DeckEntity> builder)
    {
        builder.ToTable("decks");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).IsRequired().HasMaxLength(100);
        builder.HasOne(d => d.Owner).WithMany().HasForeignKey(d => d.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(d => d.Cards).WithOne(c => c.Deck)
            .HasForeignKey(c => c.DeckId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(d => new { d.OwnerId, d.Name }).IsUnique();
    }
}

public sealed class DeckCardConfiguration : IEntityTypeConfiguration<DeckCardEntity>
{
    public void Configure(EntityTypeBuilder<DeckCardEntity> builder)
    {
        builder.ToTable("deck_cards");
        builder.HasKey(c => new { c.DeckId, c.CardId });
        builder.HasOne(c => c.Card).WithMany().HasForeignKey(c => c.CardId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GameConfiguration : IEntityTypeConfiguration<GameEntity>
{
    public void Configure(EntityTypeBuilder<GameEntity> builder)
    {
        builder.ToTable("games");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Version).IsRowVersion();
        builder.Property(g => g.StateSnapshot).HasColumnType("jsonb");
        builder.HasOne(g => g.Creator).WithMany().HasForeignKey(g => g.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(g => g.Players).WithOne(p => p.Game)
            .HasForeignKey(p => p.GameId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(g => g.Status);
    }
}

public sealed class GamePlayerConfiguration : IEntityTypeConfiguration<GamePlayerEntity>
{
    public void Configure(EntityTypeBuilder<GamePlayerEntity> builder)
    {
        builder.ToTable("game_players");
        builder.HasKey(p => new { p.GameId, p.UserId });
        builder.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Deck).WithMany().HasForeignKey(p => p.DeckId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.GameId, p.PlayerOrder }).IsUnique();
    }
}

public sealed class GameLogConfiguration : IEntityTypeConfiguration<GameLogEntity>
{
    public void Configure(EntityTypeBuilder<GameLogEntity> builder)
    {
        builder.ToTable("game_logs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Action).IsRequired().HasMaxLength(100);
        builder.Property(l => l.Result).HasMaxLength(1000);
        builder.HasOne(l => l.Game).WithMany().HasForeignKey(l => l.GameId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(l => l.GameId);
    }
}

public sealed class RecoveryTokenConfiguration : IEntityTypeConfiguration<RecoveryTokenEntity>
{
    public void Configure(EntityTypeBuilder<RecoveryTokenEntity> builder)
    {
        builder.ToTable("password_recovery_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TokenHash).IsRequired().HasMaxLength(128);
        builder.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.UserId);
    }
}
