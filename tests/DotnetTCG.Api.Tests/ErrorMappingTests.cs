using DotnetTCG.Api.Infrastructure;

namespace DotnetTCG.Api.Tests;

/// <summary>Mapeo código de error de dominio → status HTTP (ApiHelpers.StatusCode).</summary>
public sealed class ErrorMappingTests
{
    [Theory]
    [InlineData("Auth.Unauthorized", 401)]
    [InlineData("Game.Forbidden", 403)]
    [InlineData("Game.WrongPassword", 403)]
    [InlineData("Deck.NotFound", 404)]
    [InlineData("Game.Conflict", 409)]
    [InlineData("Deck.DuplicateName", 409)]
    [InlineData("Deck.InUse", 409)]
    [InlineData("User.DuplicateUsername", 409)]
    [InlineData("User.DuplicateEmail", 409)]
    [InlineData("Game.Full", 409)]                    // BUG-013b
    [InlineData("Recovery.Throttled", 429)]           // BUG-013b
    [InlineData("User.PasswordTooLong", 400)]         // BUG-011b
    [InlineData("Action.Invalid", 400)]
    public void StatusCode_MapsExpectedHttpStatus(string code, int expected)
    {
        Assert.Equal(expected, ErrorMapping.StatusCode(code));
    }
}
