using DotnetTCG.Application.Auth;
using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Application.Users;
using DotnetTCG.Infrastructure.Persistence;
using DotnetTCG.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DotnetTCG.Infrastructure.Persistence.Repositories;

public sealed class EfUserRepository(AppDbContext db) : IUserRepository
{
    public async Task<UserRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<UserRecord?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var entity = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<UserRecord?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var entity = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task AddAsync(UserRecord user, CancellationToken cancellationToken)
    {
        db.Users.Add(new UserEntity
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            PasswordHash = user.PasswordHash,
            Role = user.Role,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePasswordAsync(Guid id, string passwordHash, CancellationToken cancellationToken)
    {
        await db.Users.Where(u => u.Id == id)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.PasswordHash, passwordHash), cancellationToken);
    }

    private static UserRecord ToRecord(UserEntity entity)
        => new(entity.Id, entity.Username, entity.Email, entity.PasswordHash, entity.Role);
}

public sealed class EfRecoveryTokenRepository(AppDbContext db) : IRecoveryTokenRepository
{
    public async Task<RecoveryToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var entity = await db.RecoveryTokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
        return entity is null
            ? null
            : new RecoveryToken(entity.UserId, entity.TokenHash, entity.ExpiresAt, entity.CreatedAt);
    }

    public Task<int> CountRecentByUserAsync(Guid userId, DateTimeOffset since, CancellationToken cancellationToken)
        => db.RecoveryTokens.AsNoTracking()
            .CountAsync(t => t.UserId == userId && t.CreatedAt >= since, cancellationToken);

    public async Task SaveAsync(RecoveryToken token, CancellationToken cancellationToken)
    {
        db.RecoveryTokens.Add(new RecoveryTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = token.UserId,
            TokenHash = token.TokenHash,
            ExpiresAt = token.ExpiresAt,
            CreatedAt = token.CreatedAt,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteExpiredByUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await db.RecoveryTokens.Where(t => t.UserId == userId && t.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DeleteByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        await db.RecoveryTokens.Where(t => t.TokenHash == tokenHash)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
