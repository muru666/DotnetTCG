using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotnetTCG.Api.Hubs;
using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace DotnetTCG.Api.Tests;

public sealed class HubTests
{
    private static readonly TestFactory Factory = new();

    private sealed record IdDto(Guid Id);
    private sealed record LobbyDto(Guid Id);
    private sealed record LobbyView(List<LobbyPlayerView> Players);
    private sealed record LobbyPlayerView(Guid UserId);

    private sealed class Collector
    {
        private readonly ConcurrentBag<(string Name, JsonElement Payload, long Ticks)> _messages = [];

        public void Attach(HubConnection connection)
        {
            foreach (var name in new[] { "lobby", "game-started", "action-result", "game-state", "chat", "error" })
                connection.On<JsonElement>(name,
                    payload => _messages.Add((name, payload, Stopwatch.GetTimestamp())));
        }

        public async Task<JsonElement> WaitForAsync(
            string name, int timeoutMs = 8000, Func<JsonElement, bool>? predicate = null)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var hit = _messages.FirstOrDefault(m =>
                    m.Name == name && (predicate is null || predicate(m.Payload)));
                if (!hit.Equals(default((string, JsonElement, long))))
                    return hit.Payload;
                await Task.Delay(100);
            }

            var errors = _messages.Where(m => m.Name == "error")
                .Select(m => m.Payload.GetRawText()).ToList();
            throw new TimeoutException(
                $"No '{name}' received. Hub errors: [{string.Join("; ", errors)}]");
        }

        public async Task<JsonElement> WaitForLatestAsync(string name, int timeoutMs = 10000)
        {
            // game-state se re-emite en cada acción: esperar a que dejen de llegar
            // y devolver el último (un straggler de una acción anterior llega
            // antes de la quiescencia, nunca después).
            var sw = Stopwatch.StartNew();
            var quietSince = Stopwatch.StartNew();
            var count = 0;
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var current = _messages.Count(m => m.Name == name);
                if (current == 0)
                {
                    quietSince.Restart();
                }
                else if (current != count)
                {
                    count = current;
                    quietSince.Restart();
                }
                else if (quietSince.ElapsedMilliseconds >= 400 && count > 0)
                {
                    // El último por tiempo de recepción (ConcurrentBag no ordena).
                    return _messages.Where(m => m.Name == name).MaxBy(m => m.Ticks).Payload;
                }

                await Task.Delay(100);
            }

            throw new TimeoutException($"No stable '{name}' received.");
        }

        public void Drain(string name)
        {
            foreach (var hit in _messages.Where(m => m.Name == name).ToList())
                _messages.TryTake(out _);
        }

        public bool Has(string name) => _messages.Any(m => m.Name == name);
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

    private static async Task<string> RegisterTokenAsync(string username)
    {
        var client = Factory.CreateClient();
        username = $"{username}{Guid.NewGuid():N}"[..20];
        (await client.PostAsJsonAsync("/v1/auth/register",
            new { username, email = $"{Guid.NewGuid():N}@x.test", password = "password1" }))
            .EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/v1/auth/login",
            new { username, password = "password1" });
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<AuthTokens>())!.AccessToken;
    }

    private static async Task<HubConnection> ConnectAsync(
        WebApplicationFactory<Program> factory, string token, Collector collector)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/game", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
        collector.Attach(connection);
        await connection.StartAsync();
        return connection;
    }

    [SkippableFact]
    public async Task Connect_WithoutToken_Rejected()
    {
        if (!await DbAsync())
            return;
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/game", options =>
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler())
            .Build();

        await using (connection)
            await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [SkippableFact]
    public async Task FullFlow_MembersAndSpectator_NoLeak()
    {
        if (!await DbAsync())
            return;
        var token0 = await RegisterTokenAsync("hub0");
        var token1 = await RegisterTokenAsync("hub1");
        var spectatorToken = await RegisterTokenAsync("hubspec");
        await SeedCatalogAsync();
        var deck0 = await GoldenDeckAsync(token0);
        var deck1 = await GoldenDeckAsync(token1);

        var rest = Factory.CreateClient();
        rest.DefaultRequestHeaders.Authorization = new("Bearer", token0);
        var created = await rest.PostAsJsonAsync("/v1/games", new { isPublic = true, acceptsSpectators = true });
        created.EnsureSuccessStatusCode();
        var gameId = (await created.Content.ReadFromJsonAsync<IdDto>())!.Id;

        var c0 = new Collector();
        var c1 = new Collector();
        var cs = new Collector();
        await using var h0 = await ConnectAsync(Factory, token0, c0);
        await using var h1 = await ConnectAsync(Factory, token1, c1);
        await using var hs = await ConnectAsync(Factory, spectatorToken, cs);

        await h0.InvokeAsync("JoinLobby", gameId, null);
        await c0.WaitForAsync("lobby");
        await h1.InvokeAsync("JoinLobby", gameId, null);
        await h0.InvokeAsync("SelectDeck", gameId, deck0);
        await h1.InvokeAsync("SelectDeck", gameId, deck1);
        await h0.InvokeAsync("SetReady", gameId, true);
        await h1.InvokeAsync("SetReady", gameId, true);
        await h0.InvokeAsync("StartGame", gameId);
        await c0.WaitForAsync("game-started");
        var starter = await CurrentOrderAsync(token0, gameId);
        var firstSetup = starter == 0 ? (token0, h0, 0) : (token1, h1, 1);
        var secondSetup = starter == 0 ? (token1, h1, 1) : (token0, h0, 0);

        // Setup: cada uno coloca activo + banca + confirma, en orden de turno.
        await SetupPlayerAsync(firstSetup.Item1, firstSetup.Item2, gameId, firstSetup.Item3);
        await SetupPlayerAsync(secondSetup.Item1, secondSetup.Item2, gameId, secondSetup.Item3);
        await AssertSetupFinalizedAsync(token0, gameId);

        await hs.InvokeAsync("JoinAsSpectator", gameId);
        var specState = await cs.WaitForAsync("game-state");
        Assert.Equal(JsonValueKind.Null, specState.GetProperty("boards")[1].GetProperty("hand").ValueKind);

        // Acción por hub con el jugador al que le toca: unir energía y verificar
        // broadcast + estados personalizados (mano propia visible, rival oculta).
        // Fase de acción determinista: PassTurn siempre funciona para quien tiene
        // el turno (los PassTurn no roban carta: buscar energíaPaciente sería inútil).
        // Verifica broadcast a ambos + estados personalizados por hub.
        var before = await Authed(token0).GetFromJsonAsync<StateStatusView>($"/v1/games/{gameId}/state");
        var passerOrder = before!.CurrentPlayerIndex;
        var passerHub = passerOrder == 0 ? h0 : h1;
        var passerCollector = passerOrder == 0 ? c0 : c1;
        var otherCollector = passerOrder == 0 ? c1 : c0;
        passerCollector.Drain("action-result");
        passerCollector.Drain("game-state");
        otherCollector.Drain("action-result");
        otherCollector.Drain("game-state");
        await passerHub.InvokeAsync("ExecuteAction", gameId, new { type = "PassTurn" });

        var passerResult = await passerCollector.WaitForAsync("action-result");
        Assert.True(passerResult.GetProperty("success").GetBoolean(), "passer action-result success");
        var otherResult = await otherCollector.WaitForAsync("action-result");
        Assert.True(otherResult.GetProperty("success").GetBoolean(), "other action-result success");
        var otherState = await otherCollector.WaitForLatestAsync("game-state");
        var authoritative = await Authed(token0).GetFromJsonAsync<StateStatusView>($"/v1/games/{gameId}/state");
        Assert.True(
            otherState.GetProperty("currentPlayerIndex").GetInt32() == 1 - passerOrder,
            $"hub vio turno {otherState.GetProperty("currentPlayerIndex").GetInt32()}, " +
            $"REST dice {authoritative!.CurrentPlayerIndex}");

        // Chat llega a miembros y espectador.
        await h0.InvokeAsync("SendChat", gameId, "gl hf");
        var chat1 = await c1.WaitForAsync("chat");
        var chatS = await cs.WaitForAsync("chat");
        Assert.Equal("gl hf", chat1.GetProperty("text").GetString());
        Assert.Equal("gl hf", chatS.GetProperty("text").GetString());
    }

    [SkippableFact]
    public async Task NonMember_Action_RejectedWithoutStateChange()
    {
        if (!await DbAsync())
            return;
        var token0 = await RegisterTokenAsync("nm0");
        var intruder = await RegisterTokenAsync("intruder");
        await SeedCatalogAsync();
        var deck0 = await GoldenDeckAsync(token0);

        var rest = Factory.CreateClient();
        rest.DefaultRequestHeaders.Authorization = new("Bearer", token0);
        var created = await rest.PostAsJsonAsync("/v1/games", new { isPublic = true });
        var gameId = (await created.Content.ReadFromJsonAsync<IdDto>())!.Id;

        var ci = new Collector();
        await using var hi = await ConnectAsync(Factory, intruder, ci);

        await hi.InvokeAsync("ExecuteAction", gameId, new { type = "PassTurn" });
        var error = await ci.WaitForAsync("error");

        Assert.Equal("Game.NotFound", error.GetProperty("code").GetString());
    }

    [SkippableFact]
    public async Task Disconnect_PausesGameAfterGrace()
    {
        if (!await DbAsync())
            return;
        var fakeTime = new FakeTimeProvider();
        using var factory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<TimeProvider>(fakeTime)));
        var token0 = await RegisterTokenAsync("dc0");
        var token1 = await RegisterTokenAsync("dc1");
        await SeedCatalogAsync();
        var deck0 = await GoldenDeckAsync(token0);
        var deck1 = await GoldenDeckAsync(token1);

        var rest = factory.CreateClient();
        rest.DefaultRequestHeaders.Authorization = new("Bearer", token0);
        var created = await rest.PostAsJsonAsync("/v1/games", new { isPublic = true });
        var gameId = (await created.Content.ReadFromJsonAsync<IdDto>())!.Id;
        rest.DefaultRequestHeaders.Authorization = new("Bearer", token1);
        await rest.PostAsJsonAsync($"/v1/games/{gameId}/join", new { });
        rest.DefaultRequestHeaders.Authorization = new("Bearer", token0);
        await rest.PostAsJsonAsync($"/v1/games/{gameId}/deck", new { deckId = deck0 });
        rest.DefaultRequestHeaders.Authorization = new("Bearer", token1);
        await rest.PostAsJsonAsync($"/v1/games/{gameId}/deck", new { deckId = deck1 });
        rest.DefaultRequestHeaders.Authorization = new("Bearer", token0);
        await rest.PostAsJsonAsync($"/v1/games/{gameId}/ready", new { ready = true });
        rest.DefaultRequestHeaders.Authorization = new("Bearer", token1);
        await rest.PostAsJsonAsync($"/v1/games/{gameId}/ready", new { ready = true });
        rest.DefaultRequestHeaders.Authorization = new("Bearer", token0);
        (await rest.PostAsJsonAsync($"/v1/games/{gameId}/start", new { })).EnsureSuccessStatusCode();

        var user0Id = JwtSubject(token0);
        var c0 = new Collector();
        await using (var h0 = await ConnectAsync(factory, token0, c0))
        {
            // El juego ya empezó: Reconnect une al grupo sin exigir lobby.
            await h0.InvokeAsync("Reconnect", gameId);
            await c0.WaitForAsync("game-state");
        }

        // Esperar a que el servidor procese el disconnect. Sin esta barrera,
        // avanzar el tiempo fake antes de que exista el timer es una carrera.
        var tracker = factory.Services.GetRequiredService<GameConnectionTracker>();
        var deadline = Stopwatch.StartNew();
        while (tracker.HasConnection(user0Id, gameId)
               && deadline.Elapsed < TimeSpan.FromSeconds(10))
            await Task.Delay(200);
        Assert.False(tracker.HasConnection(user0Id, gameId));

        fakeTime.Advance(TimeSpan.FromSeconds(16));
        await Task.Delay(500);

        var blocked = await rest.PostAsJsonAsync($"/v1/games/{gameId}/actions",
            new { type = "PassTurn" });
        Assert.Contains("Game.Paused", await blocked.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task Lobby_HiddenUntilCreatorConnected()
    {
        if (!await DbAsync())
            return;
        var token0 = await RegisterTokenAsync("lc0");

        var rest = Factory.CreateClient();
        rest.DefaultRequestHeaders.Authorization = new("Bearer", token0);
        var created = await rest.PostAsJsonAsync("/v1/games", new { isPublic = true });
        var gameId = (await created.Content.ReadFromJsonAsync<IdDto>())!.Id;

        // Creador desconectado: el lobby no se lista.
        var hidden = await rest.GetFromJsonAsync<List<LobbyDto>>("/v1/games");
        Assert.Empty(hidden!);

        var c0 = new Collector();
        await using (var h0 = await ConnectAsync(Factory, token0, c0))
        {
            await h0.InvokeAsync("JoinLobby", gameId, null);
            await c0.WaitForAsync("lobby");

            var visible = await rest.GetFromJsonAsync<List<LobbyDto>>("/v1/games");
            Assert.Single(visible!);
        }

        // Creador desconectado de nuevo: el lobby vuelve a ocultarse (gracia mediante
        // sigue existiendo, pero sin conectados no se lista).
        var gone = await rest.GetFromJsonAsync<List<LobbyDto>>("/v1/games");
        Assert.Empty(gone!);
    }

    [SkippableFact]
    public async Task Disconnect_RemovesPlayerFromLobbyAfterGrace()
    {
        if (!await DbAsync())
            return;
        var fakeTime = new FakeTimeProvider();
        using var factory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<TimeProvider>(fakeTime)));
        var token0 = await RegisterTokenAsync("dl0");
        var token1 = await RegisterTokenAsync("dl1");

        var rest = factory.CreateClient();
        rest.DefaultRequestHeaders.Authorization = new("Bearer", token0);
        var created = await rest.PostAsJsonAsync("/v1/games", new { isPublic = true });
        var gameId = (await created.Content.ReadFromJsonAsync<IdDto>())!.Id;
        rest.DefaultRequestHeaders.Authorization = new("Bearer", token1);
        await rest.PostAsJsonAsync($"/v1/games/{gameId}/join", new { });

        var tracker = factory.Services.GetRequiredService<GameConnectionTracker>();
        var user0Id = JwtSubject(token0);
        var user1Id = JwtSubject(token1);
        var c0 = new Collector();
        var h0 = await ConnectAsync(factory, token0, c0);
        await h0.InvokeAsync("JoinLobby", gameId, null);
        await c0.WaitForAsync("lobby");
        var h1 = await ConnectAsync(factory, token1, new Collector());
        await h1.InvokeAsync("JoinLobby", gameId, null);
        c0.Drain("lobby");

        // Se cae el invitado: tras la gracia sale del lobby y el creador lo ve.
        await h1.DisposeAsync();
        var deadline = Stopwatch.StartNew();
        while (tracker.HasConnection(user1Id, gameId)
               && deadline.Elapsed < TimeSpan.FromSeconds(10))
            await Task.Delay(200);
        fakeTime.Advance(TimeSpan.FromSeconds(16));
        // El creador recibe el lobby actualizado por push... (puede colarse el
        // broadcast stale del join anterior con 2 jugadores: esperar el de 1).
        var updated = await c0.WaitForAsync("lobby",
            predicate: m => m.GetProperty("players").GetArrayLength() == 1);
        Assert.Equal(1, updated.GetProperty("players").GetArrayLength());
        // ...y por REST el invitado ya no esta.
        rest.DefaultRequestHeaders.Authorization = new("Bearer", token0);
        var lobbyNow = await rest.GetFromJsonAsync<LobbyView>($"/v1/games/{gameId}/lobby");
        Assert.Single(lobbyNow!.Players);

        // Se cae el creador: tras la gracia el lobby se borra.
        await h0.DisposeAsync();
        deadline.Restart();
        while (tracker.HasConnection(user0Id, gameId)
               && deadline.Elapsed < TimeSpan.FromSeconds(10))
            await Task.Delay(200);
        fakeTime.Advance(TimeSpan.FromSeconds(16));
        await Task.Delay(500);
        var gone = await rest.GetAsync($"/v1/games/{gameId}/lobby");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    private static Guid JwtSubject(string token)
    {
        var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        foreach (var claim in doc.RootElement.EnumerateObject())
        {
            if (claim.Name is "sub" or "nameid")
                return Guid.Parse(claim.Value.GetString()!);
        }

        throw new InvalidOperationException("No subject claim in token.");
    }

    private static async Task SeedCatalogAsync()
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

    private static async Task<Guid> GoldenDeckAsync(string token)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var created = await client.PostAsJsonAsync("/v1/decks", new { name = $"D{Guid.NewGuid():N}" });
        created.EnsureSuccessStatusCode();
        var deckId = (await created.Content.ReadFromJsonAsync<IdDto>())!.Id;
        for (var i = 0; i < 10; i++)
            await client.PostAsJsonAsync($"/v1/decks/{deckId}/cards",
                new { cardId = $"hx-a{i}", quantity = 3 });
        await client.PostAsJsonAsync($"/v1/decks/{deckId}/cards", new { cardId = "hx-e", quantity = 30 });
        (await client.PostAsJsonAsync($"/v1/decks/{deckId}/validate", new { })).EnsureSuccessStatusCode();
        return deckId;
    }

    private static HttpClient Authed(string token)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static async Task SetupPlayerAsync(
        string token, HubConnection hub, Guid gameId, int order)
    {
        var rest = Authed(token);
        for (var i = 0; i < 10; i++)
        {
            var state = await rest.GetFromJsonAsync<SetupView>($"/v1/games/{gameId}/state");
            var basics = state!.Boards[order].Hand!
                .Where(c => c.Subtypes.Contains("Basic")).Take(3).ToList();
            if (basics.Count > 0)
            {
                foreach (var basic in basics)
                    await hub.InvokeAsync("ExecuteAction", gameId,
                        new { type = "PlayCard", cardId = basic.Id });
                break;
            }

            await rest.PostAsJsonAsync($"/v1/games/{gameId}/mulligan", new { });
        }

        await hub.InvokeAsync("ExecuteAction", gameId, new { type = "PassTurn" });
    }

    private static async Task AssertSetupFinalizedAsync(string token, Guid gameId)
    {
        var state = await Authed(token).GetFromJsonAsync<StateStatusView>($"/v1/games/{gameId}/state");
        Assert.True(state!.Status == "InProgress",
            $"Setup no finalizó: status={state.Status} turn={state.CurrentPlayerIndex}");
    }

    private static async Task<int> CurrentOrderAsync(string token, Guid gameId)
    {
        var state = await Authed(token).GetFromJsonAsync<StateStatusView>($"/v1/games/{gameId}/state");
        return state!.CurrentPlayerIndex;
    }

    private sealed record SetupView(List<SetupBoard> Boards);
    private sealed record StateStatusView(int CurrentPlayerIndex, string Status);
    private sealed record SetupBoard(List<SetupCard>? Hand);
    private sealed record SetupCard(string Id, string Supertype, List<string> Subtypes);

    [SkippableFact]
    public async Task LeaveLobby_AfterGameStarted_KeepsGameAlive()
    {
        // Regresión FE-001: el FE navega al tablero tras game-started y dispara
        // LeaveLobby desde AMBOS jugadores; la partida no debe morir.
        if (!await DbAsync())
            return;
        var token0 = await RegisterTokenAsync("ll0");
        var token1 = await RegisterTokenAsync("ll1");
        await SeedCatalogAsync();
        var deck0 = await GoldenDeckAsync(token0);
        var deck1 = await GoldenDeckAsync(token1);

        var rest = Factory.CreateClient();
        rest.DefaultRequestHeaders.Authorization = new("Bearer", token0);
        var created = await rest.PostAsJsonAsync("/v1/games", new { isPublic = true });
        created.EnsureSuccessStatusCode();
        var gameId = (await created.Content.ReadFromJsonAsync<IdDto>())!.Id;

        var c0 = new Collector();
        var c1 = new Collector();
        await using var h0 = await ConnectAsync(Factory, token0, c0);
        await using var h1 = await ConnectAsync(Factory, token1, c1);

        await h0.InvokeAsync("JoinLobby", gameId, null);
        await c0.WaitForAsync("lobby");
        await h1.InvokeAsync("JoinLobby", gameId, null);
        await h0.InvokeAsync("SelectDeck", gameId, deck0);
        await h1.InvokeAsync("SelectDeck", gameId, deck1);
        await h0.InvokeAsync("SetReady", gameId, true);
        await h1.InvokeAsync("SetReady", gameId, true); // auto-start del hub
        await c0.WaitForAsync("game-started");

        // Exactamente lo que hace el FE al destruir la página de lobby:
        await h0.InvokeAsync("LeaveLobby", gameId);
        await h1.InvokeAsync("LeaveLobby", gameId);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(await db.Games.AnyAsync(g => g.Id == gameId), "la partida fue borrada");
            Assert.Equal(2, await db.GamePlayers.CountAsync(p => p.GameId == gameId));
        }

        var state = await rest.GetAsync($"/v1/games/{gameId}/state");
        Assert.Equal(HttpStatusCode.OK, state.StatusCode);
    }

    [SkippableFact]
    public async Task JoinAsSpectator_BeforeStart_JoinsGroup_ReceivesGameStarted()
    {
        // Regresión FE-003: con la partida aún en lobby (sin snapshot) el espectador
        // recibía Game.NotFound y nunca entraba al grupo.
        if (!await DbAsync())
            return;
        var token0 = await RegisterTokenAsync("sp0");
        var token1 = await RegisterTokenAsync("sp1");
        var specToken = await RegisterTokenAsync("spx");
        await SeedCatalogAsync();
        var deck0 = await GoldenDeckAsync(token0);
        var deck1 = await GoldenDeckAsync(token1);

        var rest = Factory.CreateClient();
        rest.DefaultRequestHeaders.Authorization = new("Bearer", token0);
        var created = await rest.PostAsJsonAsync("/v1/games",
            new { isPublic = true, acceptsSpectators = true });
        created.EnsureSuccessStatusCode();
        var gameId = (await created.Content.ReadFromJsonAsync<IdDto>())!.Id;

        var c0 = new Collector();
        var cs = new Collector();
        await using var h0 = await ConnectAsync(Factory, token0, c0);
        await using var h1 = await ConnectAsync(Factory, token1, new Collector());
        await using var hs = await ConnectAsync(Factory, specToken, cs);

        await hs.InvokeAsync("JoinAsSpectator", gameId);
        await Task.Delay(500);
        Assert.False(cs.Has("error"), "JoinAsSpectator no debe fallar en lobby");

        await h0.InvokeAsync("JoinLobby", gameId, null);
        await c0.WaitForAsync("lobby");
        await h1.InvokeAsync("JoinLobby", gameId, null);
        await h0.InvokeAsync("SelectDeck", gameId, deck0);
        await h1.InvokeAsync("SelectDeck", gameId, deck1);
        await h0.InvokeAsync("SetReady", gameId, true);
        await h1.InvokeAsync("SetReady", gameId, true);

        await cs.WaitForAsync("game-started"); // entró al grupo
        Assert.False(cs.Has("error"));
    }
}
