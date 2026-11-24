using DotnetTCG.Application.Cards;
using DotnetTCG.Application.Decks;
using DotnetTCG.Application.Games;
using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Application.Tests;

public sealed class ServiceErrorPathTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GameExecution_ActionOnGameWithoutSnapshot_Fails()
    {
        var games = new FakeGameRepository();
        var user = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        await games.AddAsync(new GameData(
            gameId, GameStatus.InProgress, 0, 0, TurnPhase.Main, 0, false, null,
            user, true, true, null, Snapshot: null, Version: 0,
            [new GamePlayerData(user, 0, null, false)], Now), CancellationToken.None);
        var exec = NewExecution(games);

        var result = await exec.ExecuteActionAsync(
            gameId, user,
            new GameActionRequest(GameActionType.PassTurn), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Game.NotStarted", result.Error!.Code);
    }

    [Fact]
    public async Task GameExecution_LoadState_MissingGame_NotFound()
    {
        var exec = NewExecution(new FakeGameRepository());

        var result = await exec.LoadGameStateAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("Game.NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task GameExecution_Start_NotReady_Rejected()
    {
        var games = new FakeGameRepository();
        var user = Guid.NewGuid();
        var game = new GameData(
            Guid.NewGuid(), GameStatus.InLobby, 0, 0, TurnPhase.Draw, 0, false, null,
            user, true, true, null, null, 0,
            [new GamePlayerData(user, 0, null, false)], Now);
        await games.AddAsync(game, CancellationToken.None);
        var exec = NewExecution(games);

        var result = await exec.StartGameAsync(game.Id, user, CancellationToken.None);

        Assert.Equal("Game.NotReady", result.Error!.Code);
    }

    [Fact]
    public async Task GameExecution_Start_ByNonMember_NotFound()
    {
        // BUG-006: antes cualquier autenticado podía iniciar el lobby.
        var games = new FakeGameRepository();
        var user = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        var game = new GameData(
            Guid.NewGuid(), GameStatus.InLobby, 0, 0, TurnPhase.Draw, 0, false, null,
            user, true, true, null, null, 0,
            [new GamePlayerData(user, 0, null, false)], Now);
        await games.AddAsync(game, CancellationToken.None);
        var exec = NewExecution(games);

        var result = await exec.StartGameAsync(game.Id, intruder, CancellationToken.None);

        Assert.Equal("Game.NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task GameExecution_Pause_WhenNotPlaying_Fails()
    {
        var games = new FakeGameRepository();
        var user = Guid.NewGuid();
        var game = new GameData(
            Guid.NewGuid(), GameStatus.InLobby, 0, 0, TurnPhase.Draw, 0, false, null,
            user, true, true, null, null, 0,
            [new GamePlayerData(user, 0, null, false)], Now);
        await games.AddAsync(game, CancellationToken.None);
        var exec = NewExecution(games);

        var result = await exec.PauseAsync(game.Id, user, CancellationToken.None);

        Assert.Equal("Game.NotJoinable", result.Error!.Code);
    }

    [Fact]
    public async Task GameExecution_Resume_WhenNotPaused_Fails()
    {
        var games = new FakeGameRepository();
        var user = Guid.NewGuid();
        var game = new GameData(
            Guid.NewGuid(), GameStatus.InProgress, 0, 0, TurnPhase.Main, 0, false, null,
            user, true, true, null, "{}", 0,
            [new GamePlayerData(user, 0, null, false)], Now);
        await games.AddAsync(game, CancellationToken.None);
        var exec = NewExecution(games);

        var result = await exec.ResumeAsync(game.Id, user, CancellationToken.None);

        Assert.Equal("Game.NotJoinable", result.Error!.Code);
    }

    [Fact]
    public async Task GameExecution_ActionByNonMember_NotFound()
    {
        var games = new FakeGameRepository();
        var user = Guid.NewGuid();
        var game = new GameData(
            Guid.NewGuid(), GameStatus.InProgress, 0, 0, TurnPhase.Main, 0, false, null,
            user, true, true, null, "{}", 0,
            [new GamePlayerData(user, 0, null, false)], Now);
        await games.AddAsync(game, CancellationToken.None);
        var exec = NewExecution(games);

        var result = await exec.ExecuteActionAsync(
            game.Id, Guid.NewGuid(),
            new GameActionRequest(GameActionType.PassTurn), CancellationToken.None);

        Assert.Equal("Game.NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task GameExecution_Mulligan_NotInSetup_Fails()
    {
        var games = new FakeGameRepository();
        var user = Guid.NewGuid();
        var game = new GameData(
            Guid.NewGuid(), GameStatus.InProgress, 0, 0, TurnPhase.Main, 0, false, null,
            user, true, true, null, "{}", 0,
            [new GamePlayerData(user, 0, null, false)], Now);
        await games.AddAsync(game, CancellationToken.None);
        var exec = NewExecution(games);

        var result = await exec.PerformMulliganAsync(game.Id, user, CancellationToken.None);

        Assert.Equal("Game.NotJoinable", result.Error!.Code);
    }

    [Fact]
    public async Task Lobby_UpdateSettings_ByNonCreator_Forbidden()
    {
        var games = new FakeGameRepository();
        var creator = Guid.NewGuid();
        var game = new GameData(
            Guid.NewGuid(), GameStatus.InLobby, 0, 0, TurnPhase.Draw, 0, false, null,
            creator, true, true, null, null, 0,
            [new GamePlayerData(creator, 0, null, false)], Now);
        await games.AddAsync(game, CancellationToken.None);
        var service = new LobbyService(games, new FakeDeckRepository(), new FakeHasher(), new TestClock(Now));

        var result = await service.UpdateSettingsAsync(
            game.Id, Guid.NewGuid(), false, false, null, CancellationToken.None);

        Assert.Equal("Game.Forbidden", result.Error!.Code);
    }

    [Fact]
    public async Task Lobby_Get_MissingGame_NotFound()
    {
        var service = new LobbyService(new FakeGameRepository(), new FakeDeckRepository(),
            new FakeHasher(), new TestClock(Now));

        var result = await service.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("Game.NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task Deck_RemoveCard_RemovesFromDeck()
    {
        var (service, _) = ServiceErrorPathHelpers.NewDeckService();
        var owner = Guid.NewGuid();
        var created = await service.CreateAsync(owner, "D", CancellationToken.None);
        await service.AddCardAsync(created.Value!.Id, owner, "xy1-p0", 4, CancellationToken.None);

        var result = await service.RemoveCardAsync(created.Value.Id, owner, "xy1-p0", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Cards);
    }

    [Fact]
    public async Task Deck_SetQuantity_NotInDeck_Fails()
    {
        var (service, _) = ServiceErrorPathHelpers.NewDeckService();
        var owner = Guid.NewGuid();
        var created = await service.CreateAsync(owner, "D", CancellationToken.None);

        var result = await service.SetQuantityAsync(
            created.Value!.Id, owner, "xy1-p0", 2, CancellationToken.None);

        Assert.Equal("Card.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task Deck_SetQuantity_Updates()
    {
        var (service, _) = ServiceErrorPathHelpers.NewDeckService();
        var owner = Guid.NewGuid();
        var created = await service.CreateAsync(owner, "D", CancellationToken.None);
        await service.AddCardAsync(created.Value!.Id, owner, "xy1-p0", 1, CancellationToken.None);

        var result = await service.SetQuantityAsync(
            created.Value.Id, owner, "xy1-p0", 3, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Cards.Single().Quantity);
    }

    [Fact]
    public async Task Deck_Rename_Updates()
    {
        var (service, _) = ServiceErrorPathHelpers.NewDeckService();
        var owner = Guid.NewGuid();
        var created = await service.CreateAsync(owner, "Old", CancellationToken.None);

        var result = await service.RenameAsync(created.Value!.Id, owner, "New", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New", result.Value!.Name);
    }

    [Fact]
    public async Task CardService_GetById_Found_ReturnsCard()
    {
        var repo = new FakeCardRepository(TestCardFactory.Catalog());
        var service = new CardService(repo, new FakePokeApiClient([]));

        var result = await service.GetByIdAsync("xy1-p0", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Poke0", result.Value!.Name);
    }

    [Fact]
    public async Task Recovery_Validate_UnknownToken_False()
    {
        var users = new FakeUserRepository();
        var service = new DotnetTCG.Application.Auth.RecoveryService(
            users, new FakeRecoveryTokens(), new FakeHasher(), new FakeSender(), new TestClock(Now));

        var result = await service.ValidateAsync("unknown", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task GameExecution_StartGame_OtherPlayersDeck_Rejected()
    {
        var cards = TestCardFactory.Catalog();
        for (var i = 0; i < 10; i++)
            cards[$"xy1-a{i}"] = TestCardFactory.Attacker($"xy1-a{i}", $"Attacker{i}");
        var cardRepo = new FakeCardRepository(cards);
        var deckRepo = new FakeDeckRepository();
        var games = new FakeGameRepository();
        var user = Guid.NewGuid();
        var foreign = Guid.NewGuid();
        var deckCards = Enumerable.Range(0, 10)
            .ToDictionary(i => $"xy1-a{i}", _ => 3);
        deckCards["xy1-e"] = 30;
        var deck = new DeckEntry(Guid.NewGuid(), foreign, "Foreign", true, 1, deckCards);
        await deckRepo.AddAsync(deck, CancellationToken.None);
        var game = new GameData(
            Guid.NewGuid(), GameStatus.InLobby, 0, 0, TurnPhase.Draw, 0, false, null,
            user, true, true, null, null, 0,
            [
                new GamePlayerData(user, 0, deck.Id, true),
                new GamePlayerData(foreign, 1, deck.Id, true),
            ], Now);
        await games.AddAsync(game, CancellationToken.None);
        var exec = ServiceErrorPathHelpers.NewExecution(games, cardRepo, deckRepo);

        var result = await exec.StartGameAsync(game.Id, user, CancellationToken.None);

        Assert.Equal("Deck.NotFound", result.Error!.Code);
    }

    private static GameExecutionService NewExecution(FakeGameRepository games)
        => ServiceErrorPathHelpers.NewExecution(games);
}

internal static class ServiceErrorPathHelpers
{
    public static (Decks.DeckService, FakeDeckRepository) NewDeckService()
    {
        var decks = new FakeDeckRepository();
        var service = new Decks.DeckService(
            decks, new FakeCardRepository(TestCardFactory.Catalog()), new Domain.Game.RuleValidator());
        return (service, decks);
    }

    public static GameExecutionService NewExecution(FakeGameRepository games)
        => NewExecution(games, new FakeCardRepository(TestCardFactory.Catalog()), new FakeDeckRepository());

    public static GameExecutionService NewExecution(
        FakeGameRepository games, FakeCardRepository cards, FakeDeckRepository decks)
    {
        var engine = new Domain.Engine.GameEngine(
            Domain.Engine.GameEngine.CreateDefault(new Random(1), new AlwaysHeadsCoinFlip()));
        return new GameExecutionService(
            games, new FakeGameLogRepository(), cards, decks, engine,
            new Infrastructure.Persistence.Snapshots.GameSnapshotMapper(),
            new AlwaysHeadsCoinFlip(),
            new TestClock(new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero)));
    }
}
