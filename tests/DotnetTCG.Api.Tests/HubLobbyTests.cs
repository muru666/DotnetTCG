using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotnetTCG.Api.Tests;

public sealed class HubLobbyTests
{
    private static readonly TestFactory Factory = new();
    private sealed record IdDto(Guid Id);

    private sealed class Bag
    {
        private readonly ConcurrentBag<(string Name, JsonElement Payload)> _messages = [];
        public void Attach(HubConnection c)
        {
            foreach (var n in new[] { "lobby", "game-started", "game-paused", "action-result", "game-state", "chat", "error" })
                c.On<JsonElement>(n, p => _messages.Add((n, p)));
        }

        public void Drain(string name)
        {
            foreach (var hit in _messages.Where(m => m.Name == name).ToList())
                _messages.TryTake(out _);
        }

        public async Task<JsonElement> WaitAsync(string name, int timeoutMs = 8000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var hit = _messages.FirstOrDefault(m => m.Name == name);
                if (!hit.Equals(default((string, JsonElement))))
                    return hit.Payload;
                await Task.Delay(100);
            }

            var errors = _messages.Where(m => m.Name == "error")
                .Select(m => m.Payload.GetRawText()).ToList();
            throw new TimeoutException($"No '{name}'. Errors: [{string.Join("; ", errors)}]");
        }
    }

    private static async Task<bool> DbAsync()
    {
        try
        {
            using var scope = Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
            return true;
        }
        catch
        {
            Skip.If(true, "Postgres de test no disponible (Docker).");
            return false;
        }
    }

    private static async Task<string> TokenAsync(string username)
    {
        var client = Factory.CreateClient();
        username = $"{username}{Guid.NewGuid():N}"[..20];
        (await client.PostAsJsonAsync("/v1/auth/register",
            new { username, email = $"{Guid.NewGuid():N}@x.test", password = "password1" }))
            .EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/v1/auth/login",
            new { username, password = "password1" });
        return (await login.Content.ReadFromJsonAsync<AuthTokens>())!.AccessToken;
    }

    private static async Task<HubConnection> ConnectAsync(string token, Bag bag)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/game", options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
        bag.Attach(connection);
        await connection.StartAsync();
        return connection;
    }

    private static async Task SeedAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICardRepository>();
        var tackle = new Attack("Tackle", string.Empty, "30", [PokemonType.Colorless]);
        var cards = new List<Card>();
        for (var i = 0; i < 10; i++)
            cards.Add(new PokemonCard($"hx-a{i}", $"HAttacker{i}", [SubType.Basic], [],
                "hx1", string.Empty, 60, 1, null, PokemonType.Colorless,
                [tackle], [], [], []));
        cards.Add(new EnergyCard("hx-e", "HFire Energy", [], [], "hx1", string.Empty,
            PokemonType.Fire, Special: null));
        await repo.UpsertRangeAsync(cards, CancellationToken.None);
    }

    private static async Task<Guid> DeckAsync(string token)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var created = await client.PostAsJsonAsync("/v1/decks", new { name = $"D{Guid.NewGuid():N}" });
        var id = (await created.Content.ReadFromJsonAsync<IdDto>())!.Id;
        for (var i = 0; i < 10; i++)
            await client.PostAsJsonAsync($"/v1/decks/{id}/cards",
                new { cardId = $"hx-a{i}", quantity = 3 });
        await client.PostAsJsonAsync($"/v1/decks/{id}/cards", new { cardId = "hx-e", quantity = 30 });
        (await client.PostAsJsonAsync($"/v1/decks/{id}/validate", new { })).EnsureSuccessStatusCode();
        return id;
    }

    [SkippableFact]
    public async Task LobbyLifecycle_ViaHub()
    {
        if (!await DbAsync())
            return;
        var t0 = await TokenAsync("hl0");
        var t1 = await TokenAsync("hl1");
        await SeedAsync();
        var deck0 = await DeckAsync(t0);
        var deck1 = await DeckAsync(t1);

        var rest = Factory.CreateClient();
        rest.DefaultRequestHeaders.Authorization = new("Bearer", t0);
        var created = await rest.PostAsJsonAsync("/v1/games",
            new { isPublic = true, acceptsSpectators = true });
        var gameId = (await created.Content.ReadFromJsonAsync<IdDto>())!.Id;

        var b0 = new Bag();
        var b1 = new Bag();
        await using var h0 = await ConnectAsync(t0, b0);
        await using var h1 = await ConnectAsync(t1, b1);

        await h0.InvokeAsync("JoinLobby", gameId, null);
        await b0.WaitAsync("lobby");
        await h1.InvokeAsync("JoinLobby", gameId, null);
        await b1.WaitAsync("lobby");

        await h0.InvokeAsync("SelectDeck", gameId, deck0);
        await h1.InvokeAsync("SelectDeck", gameId, deck1);
        await h0.InvokeAsync("SetReady", gameId, true);
        await h1.InvokeAsync("SetReady", gameId, true);
        var lobby = await b1.WaitAsync("lobby");
        Assert.Equal(2, lobby.GetProperty("players").GetArrayLength());

        await h0.InvokeAsync("StartGame", gameId);
        await b0.WaitAsync("game-started");

        await h0.InvokeAsync("PauseGame", gameId);
        var pausedEvent = await b0.WaitAsync("game-paused");
        Assert.Equal(gameId, pausedEvent.GetProperty("gameId").GetGuid());

        await h1.InvokeAsync("Reconnect", gameId);
        b1.Drain("game-state");
        var resumed = await b1.WaitAsync("game-state");
        Assert.Equal("Setup", resumed.GetProperty("status").GetString());

        // LeaveLobby con juego empezado: sale del grupo (no borra la partida en curso).
        await h1.InvokeAsync("LeaveLobby", gameId);
    }

    [SkippableFact]
    public async Task Spectator_PrivateGame_Rejected()
    {
        if (!await DbAsync())
            return;
        var t0 = await TokenAsync("sp0");
        var outsider = await TokenAsync("spX");

        var rest = Factory.CreateClient();
        rest.DefaultRequestHeaders.Authorization = new("Bearer", t0);
        var created = await rest.PostAsJsonAsync("/v1/games",
            new { isPublic = false, acceptsSpectators = false });
        var gameId = (await created.Content.ReadFromJsonAsync<IdDto>())!.Id;

        var bag = new Bag();
        await using var hs = await ConnectAsync(outsider, bag);
        await hs.InvokeAsync("JoinAsSpectator", gameId);
        var error = await bag.WaitAsync("error");

        Assert.Equal("Game.NotFound", error.GetProperty("code").GetString());
    }

    [SkippableFact]
    public async Task Chat_EmptyMessage_Rejected()
    {
        if (!await DbAsync())
            return;
        var t0 = await TokenAsync("ch0");
        var rest = Factory.CreateClient();
        rest.DefaultRequestHeaders.Authorization = new("Bearer", t0);
        var created = await rest.PostAsJsonAsync("/v1/games", new { isPublic = true });
        var gameId = (await created.Content.ReadFromJsonAsync<IdDto>())!.Id;

        var bag = new Bag();
        await using var h0 = await ConnectAsync(t0, bag);
        await h0.InvokeAsync("JoinLobby", gameId, null);
        await bag.WaitAsync("lobby");
        await h0.InvokeAsync("SendChat", gameId, "   ");
        var error = await bag.WaitAsync("error");

        Assert.Equal("Chat.Invalid", error.GetProperty("code").GetString());
    }
}
