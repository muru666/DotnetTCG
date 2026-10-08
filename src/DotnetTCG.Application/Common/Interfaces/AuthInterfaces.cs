namespace DotnetTCG.Application.Common.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenService
{
    string CreateAccessToken(Guid userId, string username, string role, DateTimeOffset now);
}
