using System.Net;
using System.Text;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Infrastructure.External;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DotnetTCG.Infrastructure.Tests;

internal sealed class StubHandler(string json) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
}

public sealed class PokeApiClientTests
{
    private const string PageJson = """
        {
          "data": [
            {
              "id": "xy1-1", "name": "Venusaur-EX", "supertype": "Pokémon",
              "subtypes": ["Basic", "EX"], "hp": "180", "types": ["Grass"],
              "retreatCost": ["Colorless", "Colorless", "Colorless", "Colorless"],
              "attacks": [
                { "name": "Poison Vine", "cost": ["Grass", "Colorless"], "damage": "60", "text": "Poison." }
              ],
              "weaknesses": [{ "type": "Fire", "value": "×2" }],
              "abilities": [{ "name": "Sunrise", "text": "Draw.", "type": "Ability" }],
              "set": { "id": "xy1" }, "images": { "small": "https://img/1" }
            },
            { "id": "xy1-121", "name": "Fire Energy", "supertype": "Energy",
              "subtypes": ["Basic"], "set": { "id": "xy1" }, "images": { "small": "https://img/e" } },
            { "id": "xy1-130", "name": "Double Colorless Energy", "supertype": "Energy",
              "subtypes": ["Special"], "set": { "id": "xy1" }, "images": { "small": "https://img/dce" } },
            { "id": "xy1-140", "name": "Tierno", "supertype": "Trainer",
              "subtypes": ["Supporter"], "rules": ["Draw 3."],
              "set": { "id": "xy1" }, "images": { "small": "https://img/t" } }
          ],
          "page": 1, "pageSize": 250, "count": 4, "totalCount": 4
        }
        """;

    [SkippableFact]
    public async Task FetchSet_MapsAllSupertypes()
    {
        var http = new HttpClient(new StubHandler(PageJson)) { BaseAddress = new Uri("https://x/") };
        var client = new PokeApiClient(http,
            Options.Create(new PokeApiOptions()), NullLogger<PokeApiClient>.Instance);

        var cards = await client.FetchSetAsync("xy1", CancellationToken.None);

        Assert.Equal(4, cards.Count);
        var pokemon = Assert.IsType<PokemonCard>(cards[0]);
        Assert.Equal(180, pokemon.Hp);
        Assert.Equal(PokemonType.Grass, pokemon.PokemonType);
        Assert.True(pokemon.IsEx);
        Assert.Single(pokemon.Attacks);
        Assert.Equal("60", pokemon.Attacks[0].Damage);
        Assert.Single(pokemon.Weaknesses);
        var energy = Assert.IsType<EnergyCard>(cards[1]);
        Assert.Equal(PokemonType.Fire, energy.EnergyType);
        Assert.Null(energy.Special);
        var dce = Assert.IsType<EnergyCard>(cards[2]);
        Assert.Equal(SpecialEnergyType.DoubleColorless, dce.Special);
        var trainer = Assert.IsType<TrainerCard>(cards[3]);
        Assert.Equal(SubType.Supporter, trainer.TrainerSubtype);
    }
}
