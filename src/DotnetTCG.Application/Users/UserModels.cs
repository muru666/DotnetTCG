namespace DotnetTCG.Application.Users;

public sealed record UserRecord(
    Guid Id,
    string Username,
    string Email,
    string PasswordHash,
    string Role);

public sealed record UserDto(Guid Id, string Username, string Email, string Role);

public sealed record AuthTokenDto(string AccessToken, DateTimeOffset ExpiresAt);
