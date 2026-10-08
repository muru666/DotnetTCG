using System.Security.Cryptography;
using System.Text;
using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Domain.Common;

namespace DotnetTCG.Application.Auth;

public interface IEmailSender
{
    Task SendRecoveryEmailAsync(string toEmail, string token, CancellationToken cancellationToken);
}

public sealed class RecoveryService(
    IUserRepository users,
    IRecoveryTokenRepository tokens,
    IPasswordHasher hasher,
    IEmailSender sender,
    IClock clock)
{
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);
    public const int MaxRequestsPerHour = 5;

    public async Task<Result<bool>> RequestAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var record = await users.FindByEmailAsync(normalizedEmail, cancellationToken);
        if (record is null)
            return Result<bool>.Success(true);

        var now = clock.UtcNow;
        var recent = await tokens.CountRecentByUserAsync(record.Id, now.AddHours(-1), cancellationToken);
        if (recent >= MaxRequestsPerHour)
            return Result<bool>.Failure(new Error("Recovery.Throttled", "Too many requests. Try later."));

        var token = GenerateToken();
        await tokens.DeleteExpiredByUserAsync(record.Id, now, cancellationToken);
        await tokens.SaveAsync(
            new RecoveryToken(record.Id, Hash(token), now.Add(TokenLifetime), now), cancellationToken);
        await sender.SendRecoveryEmailAsync(record.Email, token, cancellationToken);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ValidateAsync(string token, CancellationToken cancellationToken)
    {
        var stored = await tokens.FindByHashAsync(Hash(token), cancellationToken);
        var valid = stored is not null && stored.ExpiresAt > clock.UtcNow;
        return Result<bool>.Success(valid);
    }

    public async Task<Result<bool>> ResetAsync(
        string token, string newPassword, CancellationToken cancellationToken)
    {
        if (newPassword.Length < 8)
            return Result<bool>.Failure(new Error("User.WeakPassword", "Password needs 8+ chars."));

        var stored = await tokens.FindByHashAsync(Hash(token), cancellationToken);
        if (stored is null || stored.ExpiresAt <= clock.UtcNow)
            return Result<bool>.Failure(new Error("Recovery.InvalidToken", "Token invalid or expired."));

        var record = await users.GetByIdAsync(stored.UserId, cancellationToken);
        if (record is null)
            return Result<bool>.Failure(new Error("Recovery.InvalidToken", "Token invalid or expired."));

        await users.UpdatePasswordAsync(record.Id, hasher.Hash(newPassword), cancellationToken);
        await tokens.DeleteByHashAsync(stored.TokenHash, cancellationToken);
        return Result<bool>.Success(true);
    }

    private static string GenerateToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
