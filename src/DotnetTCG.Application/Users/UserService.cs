using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Application.Decks;
using DotnetTCG.Domain.Common;

namespace DotnetTCG.Application.Users;

public sealed class UserService(
    IUserRepository users,
    IPasswordHasher hasher,
    ITokenService tokens,
    IClock clock,
    DeckService decks)
{
    public async Task<Result<UserDto>> RegisterAsync(
        string username, string email, string password, CancellationToken cancellationToken)
    {
        username = username.Trim();
        email = email.Trim().ToLowerInvariant();
        if (username.Length < 3 || username.Length > 50)
            return Result<UserDto>.Failure(new Error("User.BadUsername", "Username must be 3-50 chars."));
        if (!email.Contains('@') || email.Length > 200)
            return Result<UserDto>.Failure(new Error("User.BadEmail", "Email is invalid."));
        if (password.Length < 8)
            return Result<UserDto>.Failure(new Error("User.WeakPassword", "Password needs 8+ chars."));

        if (await users.FindByUsernameAsync(username, cancellationToken) is not null)
            return Result<UserDto>.Failure(new Error("User.DuplicateUsername", "Username taken."));
        if (await users.FindByEmailAsync(email, cancellationToken) is not null)
            return Result<UserDto>.Failure(new Error("User.DuplicateEmail", "Email already registered."));

        var record = new UserRecord(Guid.NewGuid(), username, email, hasher.Hash(password), "User");
        var save = await users.AddAsync(record, cancellationToken);
        switch (save)
        {
            // Carrera entre el pre-chequeo y el índice único: traducir a 409, no a 500.
            case UserSaveResult.UsernameTaken:
                return Result<UserDto>.Failure(new Error("User.DuplicateUsername", "Username taken."));
            case UserSaveResult.EmailTaken:
                return Result<UserDto>.Failure(new Error("User.DuplicateEmail", "Email already registered."));
        }

        await decks.EnsureStarterDeckAsync(record.Id, cancellationToken);
        return Result<UserDto>.Success(new UserDto(record.Id, record.Username, record.Email, record.Role));
    }

    public async Task<Result<AuthTokenDto>> LoginAsync(
        string username, string password, int accessTokenMinutes, CancellationToken cancellationToken)
    {
        // Acepta usuario O email (el formulario lo promete como "Usuario o Email").
        var identifier = username.Trim();
        var record = await users.FindByUsernameAsync(identifier, cancellationToken)
            ?? await users.FindByEmailAsync(identifier.ToLowerInvariant(), cancellationToken);
        if (record is null || !hasher.Verify(password, record.PasswordHash))
            return Result<AuthTokenDto>.Failure(new Error("Auth.Unauthorized", "Invalid credentials."));

        var now = clock.UtcNow;
        var token = tokens.CreateAccessToken(record.Id, record.Username, record.Role, now);
        return Result<AuthTokenDto>.Success(
            new AuthTokenDto(token, now.AddMinutes(accessTokenMinutes)));
    }

    public async Task<Result<UserDto>> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var record = await users.GetByIdAsync(userId, cancellationToken);
        return record is null
            ? Result<UserDto>.Failure(new Error("User.NotFound", "User not found."))
            : Result<UserDto>.Success(new UserDto(record.Id, record.Username, record.Email, record.Role));
    }
}
