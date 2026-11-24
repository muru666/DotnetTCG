using DotnetTCG.Application.Auth;
using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Application.Decks;
using DotnetTCG.Application.Users;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Application.Tests;

public sealed class UserServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);

    private static (UserService Service, FakeUserRepository Users, FakeTokens Tokens) NewService()
    {
        var users = new FakeUserRepository();
        var tokens = new FakeTokens();
        return (new UserService(users, new FakeHasher(), tokens, new TestClock(Now), TestDecks.New()), users, tokens);
    }

    [Fact]
    public async Task Register_DuplicateUsername_Conflict()
    {
        var (service, _, _) = NewService();

        await service.RegisterAsync("ash", "ash@x.test", "password1", CancellationToken.None);
        var second = await service.RegisterAsync("ash", "other@x.test", "password1", CancellationToken.None);

        Assert.Equal("User.DuplicateUsername", second.Error.Code);
    }

    [Fact]
    public async Task Register_WeakPassword_Rejected()
    {
        var (service, _, _) = NewService();

        var result = await service.RegisterAsync("ash", "ash@x.test", "short", CancellationToken.None);

        Assert.Equal("User.WeakPassword", result.Error.Code);
    }

    [Fact]
    public async Task Login_WrongPassword_Unauthorized()
    {
        var (service, _, _) = NewService();
        await service.RegisterAsync("ash", "ash@x.test", "password1", CancellationToken.None);

        var result = await service.LoginAsync("ash", "wrongpass", 60, CancellationToken.None);

        Assert.Equal("Auth.Unauthorized", result.Error.Code);
    }

    [Fact]
    public async Task Login_UnknownUser_SameUnauthorized()
    {
        var (service, _, _) = NewService();

        var result = await service.LoginAsync("nobody", "whatever1", 60, CancellationToken.None);

        Assert.Equal("Auth.Unauthorized", result.Error.Code);
    }

    [Fact]
    public async Task Login_Success_ReturnsTokenWithExpiry()
    {
        var (service, _, _) = NewService();
        await service.RegisterAsync("ash", "ash@x.test", "password1", CancellationToken.None);

        var result = await service.LoginAsync("ash", "password1", 60, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Now.AddMinutes(60), result.Value!.ExpiresAt);
        Assert.Contains("ash", result.Value.AccessToken);
    }

    [Fact]
    public async Task Register_WhenUniqueIndexRace_ReturnsConflict()
    {
        // Regresión BUG-007: si dos registros simultáneos pasan el pre-chequeo, el
        // índice único chocaba y el servicio respondía 500 en vez de 409.
        var (service, users, _) = NewService();
        users.ForcedSaveResult = UserSaveResult.UsernameTaken;

        var result = await service.RegisterAsync("ash", "ash@x.test", "password1", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("User.DuplicateUsername", result.Error.Code);
    }

    [Fact]
    public async Task Login_WithEmail_Succeeds()
    {
        // FE-006: el login acepta usuario o email.
        var (service, _, _) = NewService();
        await service.RegisterAsync("ash", "ash@x.test", "password1", CancellationToken.None);

        var result = await service.LoginAsync("ash@x.test", "password1", 60, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains("ash", result.Value!.AccessToken);
    }

    [Fact]
    public async Task Login_WithWrongEmail_Unauthorized()
    {
        var (service, _, _) = NewService();
        await service.RegisterAsync("ash", "ash@x.test", "password1", CancellationToken.None);

        var result = await service.LoginAsync("mistake@x.test", "password1", 60, CancellationToken.None);

        Assert.Equal("Auth.Unauthorized", result.Error.Code);
    }
}

public sealed class RecoveryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Register_CreatesBalancedStarterDeck()
    {
        var users = new FakeUserRepository();
        var decks = new FakeDeckRepository();
        var catalog = TestCardFactory.Catalog();
        catalog["xy1-s4"] = TestCardFactory.Supporter("xy1-s4", "Supporter4");
        var deckService = new DeckService(
            decks, new FakeCardRepository(catalog), new RuleValidator());
        var service = new UserService(
            users, new FakeHasher(), new FakeTokens(), new TestClock(Now), deckService);

        var registered = await service.RegisterAsync("ash", "ash@x.test", "password1", CancellationToken.None);

        Assert.True(registered.IsSuccess);
        var owned = await decks.ListByOwnerAsync(registered.Value!.Id, CancellationToken.None);
        var starter = Assert.Single(owned);
        Assert.Equal(DeckService.StarterDeckName, starter.Name);
        Assert.True(starter.IsValid);
        Assert.Equal(60, starter.Cards.Values.Sum());
    }

    [Fact]
    public async Task Register_WithoutCatalog_StillSucceedsWithoutDeck()
    {
        var users = new FakeUserRepository();
        var decks = new FakeDeckRepository();
        var deckService = new DeckService(
            decks, new FakeCardRepository(), new RuleValidator());
        var service = new UserService(
            users, new FakeHasher(), new FakeTokens(), new TestClock(Now), deckService);

        var registered = await service.RegisterAsync("ash", "ash@x.test", "password1", CancellationToken.None);

        Assert.True(registered.IsSuccess);
        Assert.Empty(await decks.ListByOwnerAsync(registered.Value!.Id, CancellationToken.None));
    }

    private static async Task<UserRecordSetup> SetupUserAsync()
    {
        var users = new FakeUserRepository();
        var tokens = new FakeRecoveryTokens();
        var sender = new FakeSender();
        var clock = new TestClock(Now);
        var userService = new UserService(users, new FakeHasher(), new FakeTokens(), clock, TestDecks.New());
        var registered = await userService.RegisterAsync("ash", "ash@x.test", "password1", CancellationToken.None);
        var service = new RecoveryService(users, tokens, new FakeHasher(), sender, clock);
        return new UserRecordSetup(users, service, tokens, sender, clock, registered.Value!.Id);
    }

    private sealed record UserRecordSetup(
        FakeUserRepository Users,
        RecoveryService Service,
        FakeRecoveryTokens Tokens,
        FakeSender Sender,
        TestClock Clock,
        Guid UserId);

    [Fact]
    public async Task Request_UnknownEmail_SuccessWithoutSend()
    {
        var sender = new FakeSender();
        var service = new RecoveryService(new FakeUserRepository(), new FakeRecoveryTokens(),
            new FakeHasher(), sender, new TestClock(Now));

        var result = await service.RequestAsync("ghost@x.test", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task Request_KnownEmail_SendsToken()
    {
        var setup = await SetupUserAsync();

        var result = await setup.Service.RequestAsync("ash@x.test", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(setup.Sender.Sent);
        Assert.Equal(1, setup.Tokens.Count);
    }

    [Fact]
    public async Task Request_ThrottledAfterFive()
    {
        var setup = await SetupUserAsync();

        for (var i = 0; i < 5; i++)
            await setup.Service.RequestAsync("ash@x.test", CancellationToken.None);
        var sixth = await setup.Service.RequestAsync("ash@x.test", CancellationToken.None);

        Assert.Equal("Recovery.Throttled", sixth.Error.Code);
    }

    [Fact]
    public async Task Reset_FullCycle_WorksOnce()
    {
        var setup = await SetupUserAsync();
        await setup.Service.RequestAsync("ash@x.test", CancellationToken.None);
        var token = setup.Sender.Sent[0].Token;

        var valid = await setup.Service.ValidateAsync(token, CancellationToken.None);
        var reset = await setup.Service.ResetAsync(token, "newpassword", CancellationToken.None);
        var reuse = await setup.Service.ResetAsync(token, "anotherpw", CancellationToken.None);

        Assert.True(valid.Value);
        Assert.True(reset.IsSuccess);
        Assert.Equal("Recovery.InvalidToken", reuse.Error.Code);
    }

    [Fact]
    public async Task Reset_ExpiredToken_Rejected()
    {
        var setup = await SetupUserAsync();
        await setup.Service.RequestAsync("ash@x.test", CancellationToken.None);
        var token = setup.Sender.Sent[0].Token;
        setup.Clock.Current = Now.AddMinutes(16);

        var result = await setup.Service.ResetAsync(token, "newpassword", CancellationToken.None);

        Assert.Equal("Recovery.InvalidToken", result.Error.Code);
    }
}
