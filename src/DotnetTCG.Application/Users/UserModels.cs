namespace DotnetTCG.Application.Users;

public sealed record UserRecord(
    Guid Id,
    string Username,
    string Email,
    string PasswordHash,
    string Role);

public sealed record UserDto(Guid Id, string Username, string Email, string Role);

public sealed record AuthTokenDto(string AccessToken, DateTimeOffset ExpiresAt);

/// <summary>Resultado de guardar un usuario: distingue qué índice único chocó.</summary>
public enum UserSaveResult
{
    Saved,
    UsernameTaken,
    EmailTaken,
}
