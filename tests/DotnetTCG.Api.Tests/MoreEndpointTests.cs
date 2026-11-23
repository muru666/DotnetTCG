using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotnetTCG.Api.Tests;

public sealed class MoreEndpointTests
{
    private static readonly TestFactory Factory = new();

    private sealed record DeckIdDto(Guid Id);

    private static async Task<bool> DbAsync()
    {
        try
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            return true;
        }
        catch
        {
            Console.WriteLine("SKIPPED: Postgres de test no disponible (Docker).");
            return false;
        }
    }

    private static async Task<HttpClient> RegisterAsync(string username, string password = "password1")
    {
        var client = Factory.CreateClient();
        var email = $"{username}-{Guid.NewGuid():N}@x.test";
        var register = await client.PostAsJsonAsync("/v1/auth/register",
            new { username, email, password });
        register.EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/v1/auth/login", new { username, password });
        login.EnsureSuccessStatusCode();
        var tokens = (await login.Content.ReadFromJsonAsync<AuthTokens>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    private static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..24];

    [Fact]
    public async Task Register_DuplicateUsername_409()
    {
        if (!await DbAsync())
            return;
        var username = Unique("dup");
        var client = Factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@x.test";
        (await client.PostAsJsonAsync("/v1/auth/register", new { username, email, password = "password1" }))
            .EnsureSuccessStatusCode();

        var duplicate = await client.PostAsJsonAsync("/v1/auth/register",
            new { username, email = $"{Guid.NewGuid():N}@x.test", password = "password1" });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidBody_400()
    {
        if (!await DbAsync())
            return;
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/auth/register",
            new { username = "x", email = "nope", password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_InvalidBody_400()
    {
        if (!await DbAsync())
            return;
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/auth/login", new { username = "", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Recovery_FullCycle_ResetPassword()
    {
        if (!await DbAsync())
            return;
        var capture = new EmailCapture();
        using var derived = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<DotnetTCG.Application.Auth.IEmailSender>(capture)));
        var username = Unique("rec");
        var email = $"{Guid.NewGuid():N}@x.test";
        var client = derived.CreateClient();
        (await client.PostAsJsonAsync("/v1/auth/register",
            new { username, email, password = "password1" })).EnsureSuccessStatusCode();

        var requested = await client.PostAsJsonAsync("/v1/password-recovery/request", new { email });
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        Assert.NotNull(capture.LastToken);

        var validated = await client.PostAsJsonAsync("/v1/password-recovery/validate",
            new { token = capture.LastToken });
        Assert.Equal(HttpStatusCode.OK, validated.StatusCode);
        Assert.Contains("\"valid\":true", await validated.Content.ReadAsStringAsync());

        var reset = await client.PostAsJsonAsync("/v1/password-recovery/reset",
            new { token = capture.LastToken, newPassword = "newpassword1" });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var oldLogin = await client.PostAsJsonAsync("/v1/auth/login",
            new { username, password = "password1" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await client.PostAsJsonAsync("/v1/auth/login",
            new { username, password = "newpassword1" });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);

        var reuse = await client.PostAsJsonAsync("/v1/password-recovery/reset",
            new { token = capture.LastToken, newPassword = "anotherpass1" });
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
    }

    private sealed class EmailCapture : DotnetTCG.Application.Auth.IEmailSender
    {
        public string? LastToken { get; private set; }

        public Task SendRecoveryEmailAsync(string toEmail, string token, CancellationToken cancellationToken)
        {
            LastToken = token;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Recovery_RequestUnknownEmail_Still200()
    {
        if (!await DbAsync())
            return;
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/password-recovery/request",
            new { email = $"{Guid.NewGuid():N}@x.test" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Recovery_ValidateGarbage_False()
    {
        if (!await DbAsync())
            return;
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/password-recovery/validate",
            new { token = "garbage" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"valid\":false", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Deck_GetRenameDelete_Flow()
    {
        if (!await DbAsync())
            return;
        var client = await RegisterAsync(Unique("dk"));

        var created = await client.PostAsJsonAsync("/v1/decks", new { name = Unique("Deck") });
        created.EnsureSuccessStatusCode();
        var deck = (await created.Content.ReadFromJsonAsync<DeckIdDto>())!;

        var got = await client.GetAsync($"/v1/decks/{deck.Id}");
        Assert.Equal(HttpStatusCode.OK, got.StatusCode);

        var renamed = await client.PutAsJsonAsync($"/v1/decks/{deck.Id}", new { name = Unique("Renamed") });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);

        var deleted = await client.DeleteAsync($"/v1/decks/{deck.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var afterDelete = await client.GetAsync($"/v1/decks/{deck.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task Deck_CardQuantityFlow()
    {
        if (!await DbAsync())
            return;
        var client = await RegisterAsync(Unique("dq"));
        using (var scope = Factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ICardRepository>();
            await repo.UpsertRangeAsync(
                [new EnergyCard("qty-e", "Qty Energy", [], [], "zqty", "", PokemonType.Fire, null)],
                CancellationToken.None);
        }

        var created = await client.PostAsJsonAsync("/v1/decks", new { name = Unique("Deck") });
        var deck = (await created.Content.ReadFromJsonAsync<DeckIdDto>())!;
        var added = await client.PostAsJsonAsync($"/v1/decks/{deck.Id}/cards",
            new { cardId = "qty-e", quantity = 3 });
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);

        var qty = await client.PutAsJsonAsync($"/v1/decks/{deck.Id}/cards/qty-e",
            new { quantity = 5 });
        Assert.Equal(HttpStatusCode.OK, qty.StatusCode);

        var validated = await client.PostAsJsonAsync($"/v1/decks/{deck.Id}/validate", new { });
        Assert.Equal(HttpStatusCode.OK, validated.StatusCode);
        Assert.Contains("\"valid\":false", await validated.Content.ReadAsStringAsync()); // no 60

        var removed = await client.DeleteAsync($"/v1/decks/{deck.Id}/cards/qty-e");
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        var body = await removed.Content.ReadAsStringAsync();
        Assert.DoesNotContain("qty-e", body);
    }

    [Fact]
    public async Task Deck_CreateDuplicate_409()
    {
        if (!await DbAsync())
            return;
        var client = await RegisterAsync(Unique("dd"));
        var name = Unique("Same");
        (await client.PostAsJsonAsync("/v1/decks", new { name })).EnsureSuccessStatusCode();

        var duplicate = await client.PostAsJsonAsync("/v1/decks", new { name });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Cards_GetById_200And404()
    {
        if (!await DbAsync())
            return;
        var client = await RegisterAsync(Unique("ci"));
        using (var scope = Factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ICardRepository>();
            await repo.UpsertRangeAsync(
                [new EnergyCard("id-e", "Id Energy", [], [], "zid", "", PokemonType.Fire, null)],
                CancellationToken.None);
        }

        var found = await client.GetAsync("/v1/cards/id-e");
        var missing = await client.GetAsync("/v1/cards/nope");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Games_LobbyEndpoints_Flow()
    {
        if (!await DbAsync())
            return;
        var creator = await RegisterAsync(Unique("g0"));
        var joiner = await RegisterAsync(Unique("g1"));

        var created = await creator.PostAsJsonAsync("/v1/games",
            new { isPublic = true, acceptsSpectators = true });
        created.EnsureSuccessStatusCode();
        var gameId = (await created.Content.ReadFromJsonAsync<DeckIdDto>())!.Id;

        var list = await creator.GetAsync("/v1/games");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var lobby = await joiner.GetAsync($"/v1/games/{gameId}/lobby");
        Assert.Equal(HttpStatusCode.OK, lobby.StatusCode);

        await joiner.PostAsJsonAsync($"/v1/games/{gameId}/join", new { });

        var settings = await creator.PutAsJsonAsync($"/v1/games/{gameId}/settings",
            new { isPublic = false, acceptsSpectators = true, password = (string?)null });
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);

        var forbidden = await joiner.PutAsJsonAsync($"/v1/games/{gameId}/settings",
            new { isPublic = true, acceptsSpectators = true, password = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var removed = await creator.PostAsJsonAsync($"/v1/games/{gameId}/remove-player",
            new { targetUserId = ExtractUserId(joiner) });
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);

        var startNotReady = await creator.PostAsJsonAsync($"/v1/games/{gameId}/start", new { });
        Assert.Equal(HttpStatusCode.BadRequest, startNotReady.StatusCode);

        var paused = await creator.PostAsJsonAsync($"/v1/games/{gameId}/pause", new { });
        Assert.Equal(HttpStatusCode.BadRequest, paused.StatusCode);

        var left = await creator.PostAsJsonAsync($"/v1/games/{gameId}/leave", new { });
        Assert.Equal(HttpStatusCode.OK, left.StatusCode);

        var missingLobby = await creator.GetAsync($"/v1/games/{Guid.NewGuid()}/lobby");
        Assert.Equal(HttpStatusCode.NotFound, missingLobby.StatusCode);
    }

    [Fact]
    public async Task Games_StartWithReadyDecks_ThenLogsAndState()
    {
        if (!await DbAsync())
            return;
        await SeedMinimalCatalogAsync();
        var p0 = await RegisterAsync(Unique("h0"));
        var p1 = await RegisterAsync(Unique("h1"));
        var deck0 = await GoldenDeckAsync(p0, Unique("D"));
        var deck1 = await GoldenDeckAsync(p1, Unique("D"));

        var created = await p0.PostAsJsonAsync("/v1/games", new { isPublic = true });
        var gameId = (await created.Content.ReadFromJsonAsync<DeckIdDto>())!.Id;
        await p1.PostAsJsonAsync($"/v1/games/{gameId}/join", new { });
        await p0.PostAsJsonAsync($"/v1/games/{gameId}/deck", new { deckId = deck0 });
        await p1.PostAsJsonAsync($"/v1/games/{gameId}/deck", new { deckId = deck1 });
        await p0.PostAsJsonAsync($"/v1/games/{gameId}/ready", new { ready = true });
        await p1.PostAsJsonAsync($"/v1/games/{gameId}/ready", new { ready = true });
        (await p0.PostAsJsonAsync($"/v1/games/{gameId}/start", new { })).EnsureSuccessStatusCode();

        var state = await p0.GetAsync($"/v1/games/{gameId}/state");
        Assert.Equal(HttpStatusCode.OK, state.StatusCode);

        var logs = await p0.GetAsync($"/v1/games/{gameId}/logs");
        Assert.Equal(HttpStatusCode.OK, logs.StatusCode);
        Assert.Contains("GameStart", await logs.Content.ReadAsStringAsync());

        var paused = await p0.PostAsJsonAsync($"/v1/games/{gameId}/pause", new { });
        Assert.Equal(HttpStatusCode.OK, paused.StatusCode);

        var actionWhilePaused = await p1.PostAsJsonAsync($"/v1/games/{gameId}/actions",
            new { type = "PassTurn" });
        Assert.Equal(HttpStatusCode.BadRequest, actionWhilePaused.StatusCode);

        var resumed = await p1.PostAsJsonAsync($"/v1/games/{gameId}/resume", new { });
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
    }

    private static Guid ExtractUserId(HttpClient client)
    {
        // El id del usuario se lee del claim del token emitido en RegisterAsync.
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
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

    private static async Task SeedMinimalCatalogAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICardRepository>();
        var cards = new List<Card>();
        for (var i = 0; i < 10; i++)
            cards.Add(new PokemonCard($"hx-a{i}", $"HAttacker{i}", [SubType.Basic], [],
                "hx1", "", 60, 1, null, PokemonType.Colorless,
                [new Attack("Tackle", "", "30", [PokemonType.Colorless])], [], [], []));
        cards.Add(new EnergyCard("hx-e", "HFire Energy", [], [], "hx1", "",
            PokemonType.Fire, null));
        await repo.UpsertRangeAsync(cards, CancellationToken.None);
    }

    private static async Task<Guid> GoldenDeckAsync(HttpClient client, string name)
    {
        var created = await client.PostAsJsonAsync("/v1/decks", new { name });
        created.EnsureSuccessStatusCode();
        var deckId = (await created.Content.ReadFromJsonAsync<DeckIdDto>())!.Id;
        for (var i = 0; i < 10; i++)
            await client.PostAsJsonAsync($"/v1/decks/{deckId}/cards",
                new { cardId = $"hx-a{i}", quantity = 3 });
        await client.PostAsJsonAsync($"/v1/decks/{deckId}/cards",
            new { cardId = "hx-e", quantity = 30 });
        (await client.PostAsJsonAsync($"/v1/decks/{deckId}/validate", new { })).EnsureSuccessStatusCode();
        return deckId;
    }

    // ------------------------------------- regresiones de la auditoría 2026-11

    [Fact]
    public async Task Deck_CreateNameTooLong_400()
    {
        // BUG-003: el validador existía pero nunca se inyectaba (500 por varchar(100)).
        if (!await DbAsync())
            return;
        var client = await RegisterAsync(Unique("ln"));

        var response = await client.PostAsJsonAsync("/v1/decks", new { name = new string('N', 150) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deck_RenameNameTooLong_400()
    {
        // BUG-003: RenameDeckRequest ni siquiera tenía validador.
        if (!await DbAsync())
            return;
        var client = await RegisterAsync(Unique("rn"));
        var created = await client.PostAsJsonAsync("/v1/decks", new { name = Unique("D") });
        var deckId = (await created.Content.ReadFromJsonAsync<DeckIdDto>())!.Id;

        var response = await client.PutAsJsonAsync($"/v1/decks/{deckId}", new { name = new string('N', 150) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cards_PageOverflow_400()
    {
        // BUG-004: page*pageSize desbordaba int y el backend respondía 500.
        if (!await DbAsync())
            return;
        var client = await RegisterAsync(Unique("pg"));

        var response = await client.GetAsync("/v1/cards?page=30000000&pageSize=100");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deck_DeleteWhileSelectedInLobby_409()
    {
        // BUG-002: la FK de game_players convertía el delete en 500.
        if (!await DbAsync())
            return;
        await SeedMinimalCatalogAsync();
        var client = await RegisterAsync(Unique("del"));
        var deckId = await GoldenDeckAsync(client, Unique("D"));
        var created = await client.PostAsJsonAsync("/v1/games", new { isPublic = true });
        var gameId = (await created.Content.ReadFromJsonAsync<DeckIdDto>())!.Id;
        await client.PostAsJsonAsync($"/v1/games/{gameId}/deck", new { deckId });

        var response = await client.DeleteAsync($"/v1/decks/{deckId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_ConcurrentDuplicate_One201_Two409()
    {
        // BUG-007: la carrera del índice único respondía 500 en vez de 409.
        if (!await DbAsync())
            return;
        var client = Factory.CreateClient();
        var username = Unique("race");
        Task<HttpResponseMessage> Register() => client.PostAsJsonAsync("/v1/auth/register",
            new { username, email = $"{Guid.NewGuid():N}@x.test", password = "password1" });

        var results = await Task.WhenAll(Register(), Register(), Register());

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(2, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }
}
