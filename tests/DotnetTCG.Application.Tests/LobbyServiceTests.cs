using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Application.Games;
using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Application.Tests;

public sealed class LobbyServiceTests
{
    private static (LobbyService Service, FakeGameRepository Games) NewService()
    {
        var games = new FakeGameRepository();
        return (new LobbyService(games, new FakeDeckRepository(), new FakeHasher(), new TestClock(Now)), games);
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_PublicLobby()
    {
        var (service, _) = NewService();

        var result = await service.CreateAsync(Guid.NewGuid(), true, true, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Players);
    }

    [Fact]
    public async Task Join_PrivateLobby_WrongPassword_Rejected()
    {
        var (service, _) = NewService();
        var creator = Guid.NewGuid();
        var created = await service.CreateAsync(creator, false, true, "secret", CancellationToken.None);

        var wrong = await service.JoinAsync(created.Value!.Id, Guid.NewGuid(), "nope", CancellationToken.None);
        var right = await service.JoinAsync(created.Value.Id, Guid.NewGuid(), "secret", CancellationToken.None);

        Assert.Equal("Game.WrongPassword", wrong.Error.Code);
        Assert.True(right.IsSuccess);
        Assert.Equal(2, right.Value!.Players.Count);
    }

    [Fact]
    public async Task Join_FullLobby_Rejected()
    {
        var (service, _) = NewService();
        var created = await service.CreateAsync(Guid.NewGuid(), true, true, null, CancellationToken.None);
        await service.JoinAsync(created.Value!.Id, Guid.NewGuid(), null, CancellationToken.None);

        var third = await service.JoinAsync(created.Value.Id, Guid.NewGuid(), null, CancellationToken.None);

        Assert.Equal("Game.Full", third.Error.Code);
    }

    [Fact]
    public async Task SelectDeck_ForeignDeck_Rejected()
    {
        var decks = new FakeDeckRepository();
        var games = new FakeGameRepository();
        var service = new LobbyService(games, decks, new FakeHasher(), new TestClock(Now));
        var owner = Guid.NewGuid();
        var created = await service.CreateAsync(owner, true, true, null, CancellationToken.None);
        var deckService = new Decks.DeckService(decks, new FakeCardRepository(TestCardFactory.Catalog()), new Domain.Game.RuleValidator());
        var foreign = await deckService.CreateAsync(Guid.NewGuid(), "Foreign", CancellationToken.None);

        var result = await service.SelectDeckAsync(
            created.Value!.Id, owner, foreign.Value!.Id, CancellationToken.None);

        Assert.Equal("Deck.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task SetReady_WithoutDeck_Rejected()
    {
        var (service, _) = NewService();
        var owner = Guid.NewGuid();
        var created = await service.CreateAsync(owner, true, true, null, CancellationToken.None);

        var result = await service.SetReadyAsync(created.Value!.Id, owner, true, CancellationToken.None);

        Assert.Equal("Game.NoDeck", result.Error.Code);
    }

    [Fact]
    public async Task RemovePlayer_NonCreator_Forbidden()
    {
        var (service, _) = NewService();
        var created = await service.CreateAsync(Guid.NewGuid(), true, true, null, CancellationToken.None);
        var other = Guid.NewGuid();
        await service.JoinAsync(created.Value!.Id, other, null, CancellationToken.None);

        var result = await service.RemovePlayerAsync(created.Value.Id, other, other, CancellationToken.None);

        Assert.Equal("Game.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Leave_LastPlayer_DeletesGame()
    {
        var (service, games) = NewService();
        var owner = Guid.NewGuid();
        var created = await service.CreateAsync(owner, true, true, null, CancellationToken.None);

        var result = await service.LeaveAsync(created.Value!.Id, owner, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Deleted);
        Assert.Null(await games.GetByIdAsync(created.Value.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Leave_Creator_TransfersOwnership()
    {
        var (service, _) = NewService();
        var creator = Guid.NewGuid();
        var other = Guid.NewGuid();
        var created = await service.CreateAsync(creator, true, true, null, CancellationToken.None);
        await service.JoinAsync(created.Value!.Id, other, null, CancellationToken.None);

        var result = await service.LeaveAsync(created.Value.Id, creator, CancellationToken.None);

        Assert.False(result.Value!.Deleted);
        Assert.Equal(other, result.Value.Lobby!.CreatorId);
    }

    [Fact]
    public async Task CleanupStale_RemovesOldLobbies()
    {
        var (service, _) = NewService();
        await service.CreateAsync(Guid.NewGuid(), true, true, null, CancellationToken.None);

        var removed = await service.CleanupStaleAsync(Now.AddHours(25), CancellationToken.None);
        var kept = await service.ListOpenAsync(CancellationToken.None);

        Assert.Equal(1, removed);
        Assert.Empty(kept);
    }
}
