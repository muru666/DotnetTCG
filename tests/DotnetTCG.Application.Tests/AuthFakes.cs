using DotnetTCG.Application.Auth;
using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Application.Users;

namespace DotnetTCG.Application.Tests;

internal sealed class FakeUserRepository : IUserRepository
{
    private readonly Dictionary<Guid, UserRecord> _users = [];

    public Task<UserRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_users.GetValueOrDefault(id));

    public Task<UserRecord?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
        => Task.FromResult(_users.Values.FirstOrDefault(u => u.Username == username));

    public Task<UserRecord?> FindByEmailAsync(string email, CancellationToken cancellationToken)
        => Task.FromResult(_users.Values.FirstOrDefault(u => u.Email == email));

    /// <summary>Simula la carrera del índice único: AddAsync devuelve esto sin guardar.</summary>
    public UserSaveResult? ForcedSaveResult { get; set; }

    public Task<UserSaveResult> AddAsync(UserRecord user, CancellationToken cancellationToken)
    {
        if (ForcedSaveResult is { } forced)
            return Task.FromResult(forced);
        if (_users.Values.Any(u => u.Username == user.Username))
            return Task.FromResult(UserSaveResult.UsernameTaken);
        if (_users.Values.Any(u => u.Email == user.Email))
            return Task.FromResult(UserSaveResult.EmailTaken);
        _users[user.Id] = user;
        return Task.FromResult(UserSaveResult.Saved);
    }

    public Task UpdatePasswordAsync(Guid id, string passwordHash, CancellationToken cancellationToken)
    {
        _users[id] = _users[id] with { PasswordHash = passwordHash };
        return Task.CompletedTask;
    }
}

internal sealed class FakeRecoveryTokens : IRecoveryTokenRepository
{
    private readonly Dictionary<string, RecoveryToken> _tokens = [];

    public Task<RecoveryToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken)
        => Task.FromResult(_tokens.GetValueOrDefault(tokenHash));

    public Task<int> CountRecentByUserAsync(Guid userId, DateTimeOffset since, CancellationToken cancellationToken)
        => Task.FromResult(_tokens.Values.Count(t => t.UserId == userId && t.CreatedAt >= since));

    public Task SaveAsync(RecoveryToken token, CancellationToken cancellationToken)
    {
        _tokens[token.TokenHash] = token;
        return Task.CompletedTask;
    }

    public Task DeleteExpiredByUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var key in _tokens
                     .Where(kv => kv.Value.UserId == userId && kv.Value.ExpiresAt <= now)
                     .Select(kv => kv.Key).ToList())
            _tokens.Remove(key);
        return Task.CompletedTask;
    }

    public Task DeleteByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        _tokens.Remove(tokenHash);
        return Task.CompletedTask;
    }

    public int Count => _tokens.Count;
}

internal sealed class FakeHasher : IPasswordHasher
{
    public string Hash(string password) => $"HASH:{password}";
    public bool Verify(string password, string hash) => hash == $"HASH:{password}";
}

internal sealed class FakeTokens : ITokenService
{
    public string LastUsername { get; private set; } = string.Empty;

    public string CreateAccessToken(Guid userId, string username, string role, DateTimeOffset now)
    {
        LastUsername = username;
        return $"TOKEN:{userId}:{username}:{role}";
    }
}

internal sealed class FakeSender : IEmailSender
{
    public List<(string Email, string Token)> Sent { get; } = [];

    public Task SendRecoveryEmailAsync(string toEmail, string token, CancellationToken cancellationToken)
    {
        Sent.Add((toEmail, token));
        return Task.CompletedTask;
    }
}

internal static class TestDecks
{
    public static DotnetTCG.Application.Decks.DeckService New(
        FakeDeckRepository? deckRepo = null, FakeCardRepository? cardRepo = null) =>
        new(deckRepo ?? new FakeDeckRepository(),
            cardRepo ?? new FakeCardRepository(TestCardFactory.Catalog()),
            new DotnetTCG.Domain.Game.RuleValidator());
}

internal sealed class TestClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset Current { get; set; } = now;
    public DateTimeOffset UtcNow => Current;
}
