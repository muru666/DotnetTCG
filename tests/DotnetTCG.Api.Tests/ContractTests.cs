using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Infrastructure.Persistence;
using DotnetTCG.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace DotnetTCG.Api.Tests;

internal sealed record AuthTokens(string AccessToken);

internal sealed class StubPokeApi(IReadOnlyList<Card> cards) : IPokeApiClient
{
    public Task<IReadOnlyList<Card>> FetchSetAsync(string setId, CancellationToken cancellationToken)
        => Task.FromResult(cards);
}

public sealed class ContractTests
{
    private static readonly WebApplicationFactory<Program> Factory = new TestFactory();

    private static async Task<bool> PostgresAvailableAsync()
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

    private static async Task<HttpClient> UserClientAsync(string username)
    {
        username = $"{username}{Guid.NewGuid():N}"[..20];
        var client = Factory.CreateClient();
        var email = $"{username}-{Guid.NewGuid():N}@x.test";
        var register = await client.PostAsJsonAsync("/v1/auth/register",
            new { username, email, password = "password1" });
        register.EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/v1/auth/login",
            new { username, password = "password1" });
        login.EnsureSuccessStatusCode();
        var tokens = (await login.Content.ReadFromJsonAsync<AuthTokens>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    private static async Task SeedCardsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICardRepository>();
        var tackle = new Attack("Tackle", string.Empty, "30", [PokemonType.Colorless]);
        var cards = new List<Card>();
        for (var i = 0; i < 10; i++)
            cards.Add(new PokemonCard($"txy-a{i}", $"Attacker{i}", [SubType.Basic], [],
                "xy1", string.Empty, 60, 1, null, PokemonType.Colorless,
                [tackle], [], [], []));
        for (var i = 0; i < 4; i++)
            cards.Add(new TrainerCard($"txy-s{i}", $"Supporter{i}", [SubType.Supporter], [],
                "xy1", string.Empty, SubType.Supporter));
        cards.Add(new EnergyCard("txy-e", "Fire Energy", [], [], "xy1", string.Empty,
            PokemonType.Fire, Special: null));
        await repo.UpsertRangeAsync(cards, CancellationToken.None);
    }

    private static async Task<Guid> GoldenDeckAsync(HttpClient client)
    {
        var created = await client.PostAsJsonAsync("/v1/decks", new { name = $"D-{Guid.NewGuid():N}" });
        created.EnsureSuccessStatusCode();
        var deckId = (await created.Content.ReadFromJsonAsync<DeckIdDto>())!.Id;
        // Mazo 30 pokémon + 30 energías: alta prob. de básica y de energía en mano,
        // sin depender del azar del barajado (3 copias ≤ 4 por nombre; básicas ilimitadas).
        for (var i = 0; i < 10; i++)
            await client.PostAsJsonAsync($"/v1/decks/{deckId}/cards", new { cardId = $"txy-a{i}", quantity = 3 });
        await client.PostAsJsonAsync($"/v1/decks/{deckId}/cards", new { cardId = "txy-e", quantity = 30 });
        var validated = await client.PostAsJsonAsync($"/v1/decks/{deckId}/validate", new { });
        validated.EnsureSuccessStatusCode();
        return deckId;
    }

    private sealed record DeckIdDto(Guid Id);

    [Fact]
    public async Task RegisterLoginMe_RoundTrip()
    {
        if (!await PostgresAvailableAsync())
            return;
        var client = await UserClientAsync("ash");

        var me = await client.GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_401()
    {
        if (!await PostgresAvailableAsync())
            return;
        var client = Factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/register",
            new { username = $"u{Guid.NewGuid():N}", email = $"{Guid.NewGuid():N}@x.test", password = "password1" });

        var login = await client.PostAsJsonAsync("/v1/auth/login",
            new { username = "nobody", password = "wrongpass1" });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Deck_Idor_ForeignDeck_404()
    {
        if (!await PostgresAvailableAsync())
            return;
        var owner = await UserClientAsync("owner");
        var intruder = await UserClientAsync("intruder");
        var created = await owner.PostAsJsonAsync("/v1/decks", new { name = $"D-{Guid.NewGuid():N}" });
        var deckId = (await created.Content.ReadFromJsonAsync<DeckIdDto>())!.Id;

        var response = await intruder.GetAsync($"/v1/decks/{deckId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cards_Search_RejectsOversizedPage()
    {
        if (!await PostgresAvailableAsync())
            return;
        await SeedCardsAsync();
        var client = await UserClientAsync("searcher");

        var oversized = await client.GetAsync("/v1/cards?pageSize=500");
        var capped = await client.GetAsync("/v1/cards?pageSize=100");

        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
        Assert.Equal(HttpStatusCode.OK, capped.StatusCode);
        var body = await capped.Content.ReadAsStringAsync();
        Assert.Contains("\"pageSize\":100", body);
    }

    [Fact]
    public async Task Import_NonAdmin_403_Admin_200()
    {
        if (!await PostgresAvailableAsync())
            return;
        var user = await UserClientAsync("regular");

        var forbidden = await user.PostAsync("/v1/cards/import?setId=xy1", null);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new UserEntity
            {
                Id = Guid.NewGuid(),
                Username = $"admin{Guid.NewGuid():N}",
                Email = $"{Guid.NewGuid():N}@x.test",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password1"),
                Role = "Admin",
            });
            await db.SaveChangesAsync();
        }

        var stubCards = new List<Card>
        {
            new EnergyCard("stub-e", "Fire Energy", [], [], "xy1", string.Empty, PokemonType.Fire, null),
        };
        using var adminFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IPokeApiClient>(new StubPokeApi(stubCards))));
        var admin = adminFactory.CreateClient();
        var adminName = $"admin{Guid.NewGuid():N}";
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new UserEntity
            {
                Id = Guid.NewGuid(),
                Username = adminName,
                Email = $"{Guid.NewGuid():N}@x.test",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password1"),
                Role = "Admin",
            });
            await db.SaveChangesAsync();
        }

        var login = await admin.PostAsJsonAsync("/v1/auth/login",
            new { username = adminName, password = "password1" });
        login.EnsureSuccessStatusCode();
        var tokens = (await login.Content.ReadFromJsonAsync<AuthTokens>())!;
        admin.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var imported = await admin.PostAsync("/v1/cards/import?setId=xy1", null);

        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        var body = await imported.Content.ReadAsStringAsync();
        Assert.Contains("\"imported\":1", body);
    }

    [Fact]
    public async Task GameFlow_SetupAndFirstAttacks_NoLeak()
    {
        if (!await PostgresAvailableAsync())
            return;
        await SeedCardsAsync();
        var p0 = await UserClientAsync("p0");
        var p1 = await UserClientAsync("p1");
        var deck0 = await GoldenDeckAsync(p0);
        var deck1 = await GoldenDeckAsync(p1);

        var created = await p0.PostAsJsonAsync("/v1/games",
            new { isPublic = true, acceptsSpectators = true });
        created.EnsureSuccessStatusCode();
        var gameId = (await created.Content.ReadFromJsonAsync<DeckIdDto>())!.Id;
        await p1.PostAsJsonAsync($"/v1/games/{gameId}/join", new { });
        await p0.PostAsJsonAsync($"/v1/games/{gameId}/deck", new { deckId = deck0 });
        await p1.PostAsJsonAsync($"/v1/games/{gameId}/deck", new { deckId = deck1 });
        await p0.PostAsJsonAsync($"/v1/games/{gameId}/ready", new { ready = true });
        await p1.PostAsJsonAsync($"/v1/games/{gameId}/ready", new { ready = true });
        var started = await p0.PostAsJsonAsync($"/v1/games/{gameId}/start", new { });
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var summary = (await started.Content.ReadFromJsonAsync<GameSummary>())!;
        var first = summary.CurrentPlayerIndex == 0 ? (p0, 0) : (p1, 1);
        var second = summary.CurrentPlayerIndex == 0 ? (p1, 1) : (p0, 0);

        foreach (var (client, order) in new[] { first, second })
        {
            for (var i = 0; i < 10; i++)
            {
                var check = await client.GetFromJsonAsync<GameStateView>($"/v1/games/{gameId}/state");
                if (check!.Boards[order].Hand!.Any(c => c.Subtypes.Contains("Basic")))
                    break;
                await client.PostAsJsonAsync($"/v1/games/{gameId}/mulligan", new { });
            }

            var basics = (await client.GetFromJsonAsync<GameStateView>($"/v1/games/{gameId}/state"))!
                .Boards[order].Hand!.Where(c => c.Subtypes.Contains("Basic")).Take(3).ToList();
            Assert.NotEmpty(basics);
            foreach (var basic in basics)
            {
                var placed = await client.PostAsJsonAsync($"/v1/games/{gameId}/actions",
                    new { type = "PlayCard", cardId = basic.Id });
                placed.EnsureSuccessStatusCode();
            }

            var confirmed = await client.PostAsJsonAsync($"/v1/games/{gameId}/actions",
                new { type = "PassTurn" });
            confirmed.EnsureSuccessStatusCode();
        }

        var view = await p0.GetFromJsonAsync<GameStateView>($"/v1/games/{gameId}/state");
        Assert.Equal("InProgress", view!.Status);
        Assert.Null(view.Boards[1].Hand);
        Assert.True(view.Boards[1].HandCount > 0);
        Assert.NotNull(view.Boards[0].Hand);

        // Esperar un turno en que el jugador activo tenga una Energía en mano.
        // El estado se pide SIEMPRE con el cliente del jugador activo: la mano
        // propia es visible, la del rival nunca (por eso no se lee con p0).
        CardView? energy = null;
        var actingOrder = view.CurrentPlayerIndex;
        for (var turn = 0; turn < 20 && energy is null; turn++)
        {
            var acting = actingOrder == 0 ? p0 : p1;
            var state = await acting.GetFromJsonAsync<GameStateView>($"/v1/games/{gameId}/state");
            actingOrder = state!.CurrentPlayerIndex;
            var active = actingOrder == 0 ? p0 : p1;
            if (active != acting)
                state = await active.GetFromJsonAsync<GameStateView>($"/v1/games/{gameId}/state");

            energy = state!.Boards[actingOrder].Hand!.FirstOrDefault(c => c.Supertype == "Energy");
            if (energy is null)
            {
                var pass = await active.PostAsJsonAsync($"/v1/games/{gameId}/actions",
                    new { type = "PassTurn" });
                pass.EnsureSuccessStatusCode();
            }
        }

        Assert.NotNull(energy);
        var attachActor = actingOrder == 0 ? p0 : p1;
        var attach = await attachActor.PostAsJsonAsync($"/v1/games/{gameId}/actions",
            new { type = "AttachEnergy", cardId = energy.Id, target = new { zone = "active", index = 0 } });
        attach.EnsureSuccessStatusCode();

        // El otro cliente ve la energía unida al Activo del rival (sin ver su mano).
        var after = await (actingOrder == 0 ? p1 : p0)
            .GetFromJsonAsync<GameStateView>($"/v1/games/{gameId}/state");
        Assert.True(after!.Boards[actingOrder].Active!.TotalEnergy >= 1);
    }

    private sealed record GameSummary(int CurrentPlayerIndex);

    private sealed record GameStateView(
        string Status,
        int TurnNumber,
        int CurrentPlayerIndex,
        List<BoardView> Boards);
    private sealed record BoardView(
        ActiveView? Active,
        List<CardView>? Hand,
        int HandCount);
    private sealed record ActiveView(int TotalEnergy);
    private sealed record CardView(string Id, string Supertype, List<string> Subtypes);
}
