using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Application.Decks;
using DotnetTCG.Application.Games;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Engine;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Infrastructure.Persistence.Snapshots;

namespace DotnetTCG.Application.Tests;

public sealed class GameExecutionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);

    private sealed record Harness(
        LobbyService Lobby,
        GameExecutionService Execution,
        FakeGameRepository Games,
        FakeGameLogRepository Logs,
        Guid User0,
        Guid User1,
        Guid GameId);

    private static async Task<Harness> NewStartedGameAsync()
    {
        var cards = TestCardFactory.Catalog();
        for (var i = 0; i < 10; i++)
            cards[$"xy1-a{i}"] = TestCardFactory.Attacker($"xy1-a{i}", $"Attacker{i}");
        var cardRepo = new FakeCardRepository(cards);
        var deckRepo = new FakeDeckRepository();
        var games = new FakeGameRepository();
        var logs = new FakeGameLogRepository();
        var clock = new TestClock(Now);
        var lobby = new LobbyService(games, deckRepo, new FakeHasher(), clock);

        var user0 = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var deck0 = Guid.NewGuid();
        var deck1 = Guid.NewGuid();
        await deckRepo.AddAsync(
            new DeckEntry(deck0, user0, "D0", true, 1, AttackerDeck()), CancellationToken.None);
        await deckRepo.AddAsync(
            new DeckEntry(deck1, user1, "D1", true, 1, AttackerDeck()), CancellationToken.None);

        var created = await lobby.CreateAsync(user0, true, true, null, CancellationToken.None);
        var gameId = created.Value!.Id;
        await lobby.JoinAsync(gameId, user1, null, CancellationToken.None);
        await lobby.SelectDeckAsync(gameId, user0, deck0, CancellationToken.None);
        await lobby.SelectDeckAsync(gameId, user1, deck1, CancellationToken.None);
        await lobby.SetReadyAsync(gameId, user0, true, CancellationToken.None);
        await lobby.SetReadyAsync(gameId, user1, true, CancellationToken.None);

        var engine = new GameEngine(GameEngine.CreateDefault(new Random(7), new AlwaysHeadsCoinFlip()));
        var execution = new GameExecutionService(games, logs, cardRepo, deckRepo, engine,
            new GameSnapshotMapper(), new AlwaysHeadsCoinFlip(), clock);
        var started = await execution.StartGameAsync(gameId, CancellationToken.None);
        Assert.True(started.IsSuccess, started.Error?.Message);

        var loaded = await execution.LoadGameStateAsync(gameId, user0, CancellationToken.None);
        Assert.Equal(gameId, loaded.Value!.Id);

        return new Harness(lobby, execution, games, logs, user0, user1, gameId);
    }

    private static Dictionary<string, int> AttackerDeck()
    {
        var deck = new Dictionary<string, int>();
        for (var i = 0; i < 10; i++)
            deck[$"xy1-a{i}"] = 4;
        for (var i = 0; i < 4; i++)
            deck[$"xy1-s{i}"] = 4;
        deck["xy1-e"] = 4;
        return deck;
    }

    private static async Task DoSetupAsync(Harness h, Guid user)
    {
        for (var i = 0; i < 3; i++)
        {
            var state = (await h.Execution.LoadGameStateAsync(h.GameId, user, CancellationToken.None)).Value!;
            var playerIndex = user == h.User0 ? 0 : 1;
            var basic = state.Players[playerIndex].Hand.OfType<PokemonCard>().First(c => c.IsBasic);
            var result = await h.Execution.ExecuteActionAsync(h.GameId, user,
                new GameActionRequest(GameActionType.PlayCard, CardId: basic.Id), CancellationToken.None);
            Assert.True(result.IsSuccess, result.Value?.Error ?? result.Error?.Message);
        }

        var confirm = await h.Execution.ExecuteActionAsync(h.GameId, user,
            new GameActionRequest(GameActionType.PassTurn), CancellationToken.None);
        Assert.True(confirm.IsSuccess, confirm.Error?.Message);
    }

    [Fact]
    public async Task FullGame_SetupToFinished()
    {
        var h = await NewStartedGameAsync();

        foreach (var user in new[] { h.User0, h.User1 })
        {
            var loaded = await h.Execution.LoadGameStateAsync(h.GameId, user, CancellationToken.None);
            while (loaded.Value!.Players[user == h.User0 ? 0 : 1].Hand.OfType<PokemonCard>().All(c => !c.IsBasic))
            {
                await h.Execution.PerformMulliganAsync(h.GameId, user, CancellationToken.None);
                loaded = await h.Execution.LoadGameStateAsync(h.GameId, user, CancellationToken.None);
            }
        }

        await DoSetupAsync(h, h.User0);
        await DoSetupAsync(h, h.User1);

        var game = await h.Games.GetByIdAsync(h.GameId, CancellationToken.None);
        Assert.Equal(GameStatus.InProgress, game!.Status);

        var turns = 0;
        while (game.Status != GameStatus.Finished && turns < 500)
        {
            turns++;
            var user = game.Players.First(p => p.Order == game.CurrentPlayerIndex).UserId;
            var state = (await h.Execution.LoadGameStateAsync(h.GameId, user, CancellationToken.None)).Value!;
            var playerIndex = user == h.User0 ? 0 : 1;
            var board = state.Players[playerIndex];

            var energy = board.Hand.OfType<EnergyCard>().FirstOrDefault();
            if (energy is not null)
            {
                await h.Execution.ExecuteActionAsync(h.GameId, user, new GameActionRequest(
                    GameActionType.AttachEnergy, CardId: energy.Id,
                    Target: new TargetRef("active", 0)), CancellationToken.None);
            }

            var attack = board.Active!.Card.Attacks.FirstOrDefault();
            var acted = attack is null
                ? await h.Execution.ExecuteActionAsync(h.GameId, user,
                    new GameActionRequest(GameActionType.PassTurn), CancellationToken.None)
                : await h.Execution.ExecuteActionAsync(h.GameId, user,
                    new GameActionRequest(GameActionType.Attack, AttackName: attack.Name), CancellationToken.None);

            if (!acted.IsSuccess || (acted.Value!.Success == false
                && acted.Value.Error is "Attack.FirstTurn" or "Attack.EnergyShort"))
            {
                acted = await h.Execution.ExecuteActionAsync(h.GameId, user,
                    new GameActionRequest(GameActionType.PassTurn), CancellationToken.None);
            }

            Assert.True(acted.IsSuccess, acted.Error?.Message);
            Assert.True(acted.Value!.Success, acted.Value.Error);
            game = (await h.Games.GetByIdAsync(h.GameId, CancellationToken.None))!;
        }

        Assert.Equal(GameStatus.Finished, game.Status);
        Assert.NotNull(game.WinnerId);
        Assert.NotEmpty(h.Logs.Entries);
    }

    [Fact]
    public async Task PauseAndResume_RoundTrip()
    {
        var h = await NewStartedGameAsync();

        var paused = await h.Execution.PauseAsync(h.GameId, h.User0, CancellationToken.None);
        Assert.True(paused.IsSuccess);
        Assert.Equal(GameStatus.Paused, paused.Value!.Status);

        var blocked = await h.Execution.ExecuteActionAsync(h.GameId, h.User0,
            new GameActionRequest(GameActionType.PassTurn), CancellationToken.None);
        Assert.Equal("Game.Paused", blocked.Error!.Code);

        var resumed = await h.Execution.ResumeAsync(h.GameId, h.User1, CancellationToken.None);
        Assert.True(resumed.IsSuccess);
        Assert.Equal(GameStatus.Setup, resumed.Value!.Status);
    }

    [Fact]
    public async Task ExecuteAction_ConcurrentConflict_Retries()
    {
        var h = await NewStartedGameAsync();
        h.Games.ThrowConflictOnce = true;

        var result = await h.Execution.PerformMulliganAsync(h.GameId, h.User0, CancellationToken.None);

        Assert.True(result.IsSuccess || result.Error!.Code == "Game.NoMulligan");
    }
}
