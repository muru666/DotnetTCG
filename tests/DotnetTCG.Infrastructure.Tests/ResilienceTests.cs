using System.Net;
using System.Text;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Infrastructure.Auth;
using DotnetTCG.Infrastructure.Email;
using DotnetTCG.Infrastructure.External;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DotnetTCG.Infrastructure.Tests;

internal sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
{
    private int _call;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = responses[Math.Min(_call++, responses.Length - 1)];
        return Task.FromResult(response.Clone());
    }
}

internal static class HttpResponseMessageExtensions
{
    public static HttpResponseMessage Clone(this HttpResponseMessage original) => new(original.StatusCode)
    {
        Content = new StringContent(
            original.Content.ReadAsStringAsync().GetAwaiter().GetResult(),
            Encoding.UTF8, "application/json"),
    };
}

internal static class PokeJson
{
    public static string Page(int count, int totalCount, int page = 1, int pageSize = 250)
        => $$"""
        {
          "data": [{{string.Join(",", Enumerable.Range(0, count).Select(i =>
            $"{{\"id\":\"xy1-{page * 100 + i}\",\"name\":\"Card{page * 100 + i}\",\"supertype\":\"Trainer\",\"subtypes\":[\"Item\"],\"set\":{{\"id\":\"xy1\"}},\"images\":{{\"small\":\"img\"}}}}"))}}],
          "page": {{page}}, "pageSize": {{pageSize}}, "count": {{count}}, "totalCount": {{totalCount}}
        }
        """;

    public const string Empty = """
        {"data": [], "page": 1, "pageSize": 250, "count": 0, "totalCount": 0}
        """;
}

public sealed class PokeApiClientResilienceTests
{
    private static PokeApiClient Client(HttpMessageHandler handler, string? apiKey = null)
        => new(new HttpClient(handler) { BaseAddress = new Uri("https://x/") },
            Options.Create(new PokeApiOptions { ApiKey = apiKey }),
            NullLogger<PokeApiClient>.Instance);

    [SkippableFact]
    public async Task Fetch_RetriesOn500_ThenSucceeds()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PokeJson.Page(1, 1)) });

        var cards = await Client(handler).FetchSetAsync("xy1", CancellationToken.None);

        Assert.Single(cards);
    }

    [SkippableFact]
    public async Task Fetch_PersistentFailure_ThrowsAfterThreeAttempts()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Client(handler).FetchSetAsync("xy1", CancellationToken.None));
    }

    [SkippableFact]
    public async Task Fetch_PaginatesUntilTotalCount()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(PokeJson.Page(250, 300, page: 1)),
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(PokeJson.Page(50, 300, page: 2)),
            });

        var cards = await Client(handler).FetchSetAsync("xy1", CancellationToken.None);

        Assert.Equal(300, cards.Count);
    }

    [SkippableFact]
    public async Task Fetch_WithApiKey_SendsHeader()
    {
        string? seenKey = null;
        var handler = new DelegateHandler(req =>
        {
            seenKey = req.Headers.TryGetValues("X-Api-Key", out var v) ? v.First() : null;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(PokeJson.Empty),
            };
        });

        await Client(handler, "secret-key").FetchSetAsync("xy1", CancellationToken.None);

        Assert.Equal("secret-key", seenKey);
    }

    [SkippableFact]
    public async Task Fetch_NoData_DoesNotLoop()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PokeJson.Empty) });

        var cards = await Client(handler).FetchSetAsync("xy1", CancellationToken.None);

        Assert.Empty(cards);
    }

    [SkippableFact]
    public async Task Fetch_MapsWeaknessesResistancesAndRules()
    {
        const string json = """
        {
          "data": [
            {
              "id": "xy1-9", "name": "Simoneon", "supertype": "Pokémon",
              "subtypes": ["Stage 1"], "hp": "90", "types": ["Psychic"],
              "evolvesFrom": "Base",
              "rules": ["Some rule text."],
              "weaknesses": [{ "type": "Darkness", "value": "×2" }],
              "resistances": [{ "type": "Psychic", "value": "-20" }],
              "set": { "id": "xy1" }, "images": { "small": "https://img/9" }
            },
            { "id": "xy1-r", "name": "Rainbow Energy", "supertype": "Energy",
              "subtypes": ["Special"], "set": { "id": "xy1" },
              "images": { "small": "https://img/r" } }
          ],
          "page": 1, "pageSize": 250, "count": 2, "totalCount": 2
        }
        """;
        var handler = new StubHandler(json);

        var cards = await new PokeApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://x/") },
            Options.Create(new PokeApiOptions()), NullLogger<PokeApiClient>.Instance)
            .FetchSetAsync("xy1", CancellationToken.None);

        var pokemon = Assert.IsType<PokemonCard>(cards[0]);
        Assert.Equal("Base", pokemon.EvolvesFrom);
        Assert.Single(pokemon.Weaknesses);
        Assert.Equal(PokemonType.Darkness, pokemon.Weaknesses[0].Type);
        Assert.Single(pokemon.Resistances);
        Assert.Equal("-20", pokemon.Resistances[0].Reduction);
        Assert.Single(pokemon.Rules);
        var rainbow = Assert.IsType<EnergyCard>(cards[1]);
        Assert.Equal(SpecialEnergyType.Rainbow, rainbow.Special);
    }

    [SkippableFact]
    public async Task Fetch_UnknownSupertype_Throws()
    {
        const string json = """
        {"data": [{"id": "x-1", "name": "Weird", "supertype": "TotallyNew",
          "subtypes": [], "set": {"id": "s"}, "images": {"small": "i"}}],
         "page": 1, "pageSize": 250, "count": 1, "totalCount": 1}
        """;
        var handler = new StubHandler(json);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PokeApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://x/") },
                Options.Create(new PokeApiOptions()), NullLogger<PokeApiClient>.Instance)
                .FetchSetAsync("s", CancellationToken.None));
    }
}

internal sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(respond(request));
}

public sealed class SmtpEmailSenderTests
{
    [SkippableFact]
    public async Task Send_WithoutSmtpConfig_ThrowsFast()
    {
        var sender = new SmtpEmailSender(
            Options.Create(new SmtpOptions { Host = "" }),
            NullLogger<SmtpEmailSender>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendRecoveryEmailAsync("a@b.c", "token", CancellationToken.None));

        Assert.Contains("SMTP not configured", ex.Message);
    }
}

public sealed class InfrastructureDependencyInjectionTests
{
    [SkippableFact]
    public void AddInfrastructure_WithoutConnectionString_Throws()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(config));
    }

    [SkippableFact]
    public void AddInfrastructure_WithConnectionString_RegistersCoreServices()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Host=localhost;Port=1;Database=x;Username=u;Password=p",
        }).Build();

        services.AddInfrastructure(config);

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<Application.Common.Interfaces.IClock>());
        Assert.NotNull(provider.GetService<Domain.Game.ICoinFlipService>());
        Assert.NotNull(provider.GetService<Application.Common.Interfaces.IPasswordHasher>());
        Assert.NotNull(provider.GetService<Application.Common.Interfaces.ITokenService>());
        Assert.NotNull(provider.GetService<Application.Common.Interfaces.ICardRepository>());
        Assert.NotNull(provider.GetService<Application.Common.Interfaces.IGameRepository>());
        Assert.NotNull(provider.GetService<Domain.Engine.GameEngine>());
    }
}
