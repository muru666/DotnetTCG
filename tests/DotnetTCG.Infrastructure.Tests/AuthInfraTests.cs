using System.IdentityModel.Tokens.Jwt;
using DotnetTCG.Application.Auth;
using DotnetTCG.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace DotnetTCG.Infrastructure.Tests;

public sealed class AuthInfraTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BCrypt_RoundTrip()
    {
        var hasher = new BCryptPasswordHasher();

        var hash = hasher.Hash("password1");

        Assert.True(hasher.Verify("password1", hash));
        Assert.False(hasher.Verify("wrong", hash));
        Assert.False(hasher.Verify("password1", "not-a-hash"));
    }

    [Fact]
    public void JwtToken_CarriesExpectedClaims()
    {
        var service = new JwtTokenService(Options.Create(new JwtOptions
        {
            Key = "TEST-ONLY-not-a-real-secret-0123456789abcdef",
            Issuer = "DotnetTCG",
            Audience = "DotnetTCG",
            AccessTokenMinutes = 60,
        }));
        var userId = Guid.NewGuid();

        var token = service.CreateAccessToken(userId, "ash", "User", Now);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(userId.ToString(), jwt.Subject);
        Assert.Equal("ash", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.UniqueName).Value);
        Assert.Equal(Now.AddMinutes(60), jwt.ValidTo);
    }
}
