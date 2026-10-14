using System.Net;
using System.Net.Http.Json;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotnetTCG.Api.Tests;

public sealed class SmallEndpointTests
{
    private static readonly TestFactory Factory = new();

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

    private static async Task<HttpClient> RegisterAsync()
    {
        var client = Factory.CreateClient();
        var username = $"sm{Guid.NewGuid():N}"[..20];
        (await client.PostAsJsonAsync("/v1/auth/register",
            new { username, email = $"{Guid.NewGuid():N}@x.test", password = "password1" }))
            .EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/v1/auth/login",
            new { username, password = "password1" });
        var tokens = (await login.Content.ReadFromJsonAsync<AuthTokens>())!;
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", tokens.AccessToken);
        return client;
    }

    [Fact]
    public async Task Cards_UnknownType_400()
    {
        if (!await DbAsync())
            return;
        var client = await RegisterAsync();

        var response = await client.GetAsync("/v1/cards?type=NotAType");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cards_EmptySearch_200EmptyPage()
    {
        if (!await DbAsync())
            return;
        var client = await RegisterAsync();

        var response = await client.GetAsync("/v1/cards?name=zzzz-no-match");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"total\":0", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Decks_List_ReturnsArray()
    {
        if (!await DbAsync())
            return;
        var client = await RegisterAsync();
        (await client.PostAsJsonAsync("/v1/decks", new { name = $"L{Guid.NewGuid():N}" }))
            .EnsureSuccessStatusCode();

        var response = await client.GetAsync("/v1/decks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Games_MissingGameEndpoints_404()
    {
        if (!await DbAsync())
            return;
        var client = await RegisterAsync();
        var missing = Guid.NewGuid();

        var lobby = await client.GetAsync($"/v1/games/{missing}/lobby");
        var logs = await client.GetAsync($"/v1/games/{missing}/logs");
        var state = await client.GetAsync($"/v1/games/{missing}/state");
        var join = await client.PostAsJsonAsync($"/v1/games/{missing}/join", new { });
        var remove = await client.PostAsJsonAsync($"/v1/games/{missing}/remove-player",
            new { targetUserId = Guid.NewGuid() });
        var mulligan = await client.PostAsJsonAsync($"/v1/games/{missing}/mulligan", new { });

        Assert.Equal(HttpStatusCode.NotFound, lobby.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, logs.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, state.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, join.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, mulligan.StatusCode);
    }

    [Fact]
    public async Task Games_UnknownActionType_400()
    {
        if (!await DbAsync())
            return;
        var client = await RegisterAsync();
        var created = await client.PostAsJsonAsync("/v1/games", new { isPublic = true });
        var gameId = (await created.Content.ReadFromJsonAsync<IdDto>())!.Id;

        var response = await client.PostAsJsonAsync($"/v1/games/{gameId}/actions",
            new { type = "Teleport" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Teleport", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_RateLimited_Returns429()
    {
        if (!await DbAsync())
            return;
        using var factory = Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:AuthPermitLimit", "3"));
        var client = factory.CreateClient();

        HttpStatusCode? last = null;
        for (var i = 0; i < 4; i++)
        {
            var response = await client.PostAsJsonAsync("/v1/auth/login",
                new { username = "nobody", password = "wrongpass1" });
            last = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }

    private sealed record IdDto(Guid Id);
}
