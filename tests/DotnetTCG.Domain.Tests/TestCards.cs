using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Domain.Tests;

internal static class TestCards
{
    public static PokemonCard BasicPokemon(
        string id,
        string name,
        int hp,
        PokemonType type = PokemonType.Colorless,
        int retreatCost = 1,
        IReadOnlyList<Attack>? attacks = null)
        => new(
            id,
            name,
            [SubType.Basic],
            [],
            "xy1",
            string.Empty,
            hp,
            retreatCost,
            EvolvesFrom: null,
            type,
            attacks ?? [],
            [],
            [],
            []);

    public static PokemonCard Stage1Pokemon(string id, string name, string evolvesFrom, int hp)
        => new(
            id,
            name,
            [SubType.Stage1],
            [],
            "xy1",
            string.Empty,
            hp,
            1,
            evolvesFrom,
            PokemonType.Colorless,
            [],
            [],
            [],
            []);

    public static EnergyCard BasicEnergy(string id, PokemonType type)
        => new(id, $"{type} Energy", [], [], "xy1", string.Empty, type, Special: null);

    public static EnergyCard DoubleColorless(string id = "xy1-130")
        => new(id, "Double Colorless Energy", [], [], "xy1", string.Empty,
            PokemonType.Colorless, SpecialEnergyType.DoubleColorless);

    public static TrainerCard Supporter(string id, string name)
        => new(id, name, [SubType.Supporter], [], "xy1", string.Empty, SubType.Supporter);

    public static TrainerCard Stadium(string id, string name)
        => new(id, name, [SubType.Stadium], [], "xy1", string.Empty, SubType.Stadium);

    public static TrainerCard Tool(string id, string name)
        => new(id, name, [SubType.Tool], [], "xy1", string.Empty, SubType.Tool);

    public static TrainerCard Item(string id, string name)
        => new(id, name, [SubType.Item], [], "xy1", string.Empty, SubType.Item);

    public static PokemonCard Attacker(string id, string name, int hp = 60)
        => new(id, name, [SubType.Basic], [], "xy1", string.Empty,
            hp, 1, EvolvesFrom: null, PokemonType.Colorless,
            [new Attack("Tackle", string.Empty, "30", [PokemonType.Colorless])],
            [], [], []);
}
