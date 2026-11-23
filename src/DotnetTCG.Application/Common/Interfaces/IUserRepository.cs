using DotnetTCG.Application.Users;

namespace DotnetTCG.Application.Common.Interfaces;

public interface IUserRepository
{
    Task<UserRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<UserRecord?> FindByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<UserRecord?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task<UserSaveResult> AddAsync(UserRecord user, CancellationToken cancellationToken);
    Task UpdatePasswordAsync(Guid id, string passwordHash, CancellationToken cancellationToken);
}

public sealed record RecoveryToken(Guid UserId, string TokenHash, DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt);

public interface IRecoveryTokenRepository
{
    Task<RecoveryToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task<int> CountRecentByUserAsync(Guid userId, DateTimeOffset since, CancellationToken cancellationToken);
    Task SaveAsync(RecoveryToken token, CancellationToken cancellationToken);
    Task DeleteExpiredByUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
    Task DeleteByHashAsync(string tokenHash, CancellationToken cancellationToken);
}
