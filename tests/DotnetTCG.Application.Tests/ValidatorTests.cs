using DotnetTCG.Application.Auth;
using DotnetTCG.Application.Cards;
using DotnetTCG.Application.Decks;
using DotnetTCG.Application.Users;
using FluentValidation.TestHelper;

namespace DotnetTCG.Application.Tests;

public sealed class ValidatorTests
{
    [Theory]
    [InlineData("", "a@b.c", "password1")]
    [InlineData("ab", "a@b.c", "password1")]
    [InlineData("ash", "not-an-email", "password1")]
    [InlineData("ash", "a@b.c", "short")]
    public void Register_Invalid_Cases(string username, string email, string password)
    {
        var validator = new RegisterValidator();

        var result = validator.TestValidate(new RegisterRequest(username, email, password));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Register_Valid_Passes()
    {
        var validator = new RegisterValidator();

        var result = validator.TestValidate(new RegisterRequest("ash", "a@b.c", "password1"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Login_Empty_Fails()
    {
        var validator = new LoginValidator();

        Assert.False(validator.TestValidate(new LoginRequest("", "")).IsValid);
        Assert.True(validator.TestValidate(new LoginRequest("ash", "pw")).IsValid);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("xy1-1", "")]
    [InlineData("", "password1")]
    public void RecoveryReset_Invalid_Cases(string token, string password)
    {
        var validator = new RecoveryResetValidator();

        Assert.False(validator.TestValidate(new RecoveryResetRequest(token, password)).IsValid);
    }

    [Fact]
    public void RecoveryRequest_BadEmail_Fails()
    {
        var validator = new RecoveryRequestValidator();

        Assert.False(validator.TestValidate(new RecoveryRequest("nope")).IsValid);
        Assert.True(validator.TestValidate(new RecoveryRequest("a@b.c")).IsValid);
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("deck", 0)]
    [InlineData("deck", 61)]
    public void AddCard_Invalid_Cases(string cardId, int quantity)
    {
        var validator = new AddCardValidator();

        Assert.False(validator.TestValidate(new AddCardRequest(cardId, quantity)).IsValid);
    }

    [Fact]
    public void SearchCards_PageAndSizeBounds()
    {
        var validator = new SearchCardsValidator();

        Assert.False(validator.TestValidate(new SearchCardsQuery(null, null, null, -1, 10)).IsValid);
        Assert.False(validator.TestValidate(new SearchCardsQuery(null, null, null, 0, 0)).IsValid);
        Assert.False(validator.TestValidate(new SearchCardsQuery(null, null, null, 0, 101)).IsValid);
        Assert.True(validator.TestValidate(new SearchCardsQuery(null, null, null, 0, 100)).IsValid);
    }

    [Fact]
    public void CreateDeck_NameBounds()
    {
        var validator = new CreateDeckValidator();

        Assert.False(validator.TestValidate(new CreateDeckRequest("")).IsValid);
        Assert.False(validator.TestValidate(new CreateDeckRequest(new string('x', 101))).IsValid);
        Assert.True(validator.TestValidate(new CreateDeckRequest("My Deck")).IsValid);
    }
}
