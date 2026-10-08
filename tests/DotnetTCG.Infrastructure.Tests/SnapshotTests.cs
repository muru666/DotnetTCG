using System.Text.Json;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;
using DotnetTCG.Infrastructure.Persistence.Snapshots;

namespace DotnetTCG.Infrastructure.Tests;

public sealed class SnapshotTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static Dictionary<string, Card> Catalog()
    {
        var cards = new Dictionary<string, Card>();
        var tackle = new Attack("Tackle", string.Empty, "30", [PokemonType.Colorless]);
        for (var i = 0; i < 4; i++)
        {
            cards[$"p{i}"] = new PokemonCard($"p{i}", $"Poke{i}", [SubType.Basic], [],
                "xy1", string.Empty, 100, 1, null, PokemonType.Fire, [tackle], [], [], []);
        }

        cards["e1"] = new EnergyCard("e1", "Fire Energy", [], [], "xy1", string.Empty,
            PokemonType.Fire, Special: null);
        cards["dce"] = new EnergyCard("dce", "Double Colorless Energy", [], [], "xy1", string.Empty,
            PokemonType.Colorless, SpecialEnergyType.DoubleColorless);
        cards["tool"] = new TrainerCard("tool", "Muscle Band", [SubType.Tool], [], "xy1", string.Empty,
            SubType.Tool);
        cards["stadium"] = new TrainerCard("stadium", "Fairy Garden", [SubType.Stadium], [], "xy1",
            string.Empty, SubType.Stadium);
        cards["supporter"] = new TrainerCard("supporter", "Tierno", [SubType.Supporter], [], "xy1",
            string.Empty, SubType.Supporter);
        return cards;
    }

    private static GameState MidGameState(Dictionary<string, Card> cards)
    {
        var state = new GameState(new PlayerBoard(), new PlayerBoard(), 1, suddenDeath: false);
        state.SetStatus(GameStatus.InProgress);
        var pokemon = (PokemonCard)cards["p0"];
        var all = new List<Card>();
        for (var i = 0; i < 4; i++)
            all.Add(cards[$"p{i}"]);
        all.AddRange(Enumerable.Repeat(cards["e1"], 30));
        all.AddRange(Enumerable.Repeat(cards["dce"], 10));
        all.AddRange(Enumerable.Repeat(cards["supporter"], 16));

        foreach (var board in state.Players)
        {
            board.LoadDeckForTests(all);
            for (var i = 0; i < 5; i++)
                board.DrawCard();
            board.PlaceActive(new PokemonInPlay(pokemon));
            board.AddToBench(new PokemonInPlay((PokemonCard)cards["p1"]));
            board.InitPrizeCards(6);
        }

        var active = state.Players[0].Active!;
        active.AttachEnergy((EnergyCard)cards["e1"]);
        active.AttachEnergy((EnergyCard)cards["dce"]);
        active.AttachTool((TrainerCard)cards["tool"]);
        active.AddDamage(45);
        active.AddStatus(Status.Burned);
        active.AddModifier(new BoardModifier(ModifierType.DamageBoost, "Muscle Band", 20,
            ModifierDuration.Permanent, TurnApplied: 2));
        state.Players[0].AddModifier(new BoardModifier(ModifierType.RestrictItems, "Effect", 0,
            ModifierDuration.UntilNextTurn, TurnApplied: 3));
        state.SetStadium((TrainerCard)cards["stadium"]);
        state.RegisterEnergyAttached();
        state.RegisterSupporterPlayed();
        state.SetPendingChoice(new PendingChoice("attack:Tackle:bench-target:opp", 0, null,
            "Choose.", [new ChoiceOption("0", "Poke1")], "Tackle"));
        state.SetPendingAttack(new PendingAttack("Tackle", 0));
        state.AdvanceTurn();
        return state;
    }

    [Fact]
    public void RoundTrip_MidGame_PreservesEverything()
    {
        var catalog = Catalog();
        var mapper = new GameSnapshotMapper();
        var original = MidGameState(catalog);

        var json = mapper.Serialize(original);
        var restored = mapper.Deserialize(json, catalog);
        var rejson = mapper.Serialize(restored);

        Assert.Equal(
            JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(json), Json),
            JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(rejson), Json));
        Assert.Equal(original.Players[0].Active!.TotalDamage, restored.Players[0].Active!.TotalDamage);
        Assert.Equal(3, restored.Players[0].Active.TotalEnergyCount);
        Assert.Equal(Status.Burned, restored.Players[0].Active.Statuses.Single());
        Assert.Equal("stadium", restored.StadiumCard!.Id);
        Assert.Equal(6, restored.Players[1].PrizeCount);
        Assert.NotNull(restored.PendingChoice);
        Assert.NotNull(restored.PendingAttack);
        Assert.Equal(2, restored.TurnNumber);
        Assert.Equal(original.SupportersPlayedThisTurn, restored.SupportersPlayedThisTurn);
        Assert.Equal(original.EnergiesAttachedThisTurn, restored.EnergiesAttachedThisTurn);
    }

    [Fact]
    public void CollectCardIds_CoversAllZones()
    {
        var catalog = Catalog();
        var mapper = new GameSnapshotMapper();
        var json = mapper.Serialize(MidGameState(catalog));

        var ids = mapper.CollectCardIds(json);

        Assert.Contains("p0", ids);
        Assert.Contains("e1", ids);
        Assert.Contains("dce", ids);
        Assert.Contains("tool", ids);
        Assert.Contains("stadium", ids);
        Assert.Contains("supporter", ids);
    }

    [Fact]
    public void Deserialize_MissingCard_Throws()
    {
        var catalog = Catalog();
        var mapper = new GameSnapshotMapper();
        var json = mapper.Serialize(MidGameState(catalog));
        catalog.Remove("e1");

        Assert.Throws<InvalidOperationException>(() => mapper.Deserialize(json, catalog));
    }
}
