using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Engine.Handlers;

public sealed class PlayPokemonHandler(EngineServices services)
{
    public string? Handle(GameState state, GameAction action)
    {
        if (action.Card is not PokemonCard card)
            return "Play.CardRequired";

        var error = services.Rules.CanPlayPokemonToBench(state, action.PlayerIndex, card);
        if (error is not null)
            return error.Code;

        var board = state.Players[action.PlayerIndex];
        board.RemoveFromHand(card);
        board.AddToBench(new PokemonInPlay(card));
        return null;
    }
}

public sealed class EvolveHandler(EngineServices services)
{
    public string? Handle(GameState state, GameAction action)
    {
        if (action.Card is not PokemonCard evolution)
            return "Evolve.CardRequired";
        if (action.Target is null)
            return "Evolve.TargetRequired";

        var error = services.Rules.CanEvolve(state, action.Target, evolution);
        if (error is not null)
            return error.Code;

        state.Players[action.PlayerIndex].RemoveFromHand(evolution);
        action.Target.EvolveInto(evolution);
        return null;
    }
}

public sealed class AttachEnergyHandler(EngineServices services)
{
    public string? Handle(GameState state, GameAction action)
    {
        if (action.Card is not EnergyCard energy)
            return "Energy.CardRequired";
        if (action.Target is null)
            return "Energy.TargetRequired";

        var error = services.Rules.CanAttachEnergy(state);
        if (error is not null)
            return error.Code;

        var board = state.Players[action.PlayerIndex];
        if (!OwnsPokemon(board, action.Target))
            return "Energy.ForeignTarget";
        board.RemoveFromHand(energy);
        action.Target.AttachEnergy(energy);
        state.RegisterEnergyAttached();

        if (energy.Special == SpecialEnergyType.Rainbow)
            action.Target.AddDamage(10);

        return null;
    }

    private static bool OwnsPokemon(PlayerBoard board, PokemonInPlay pokemon)
        => board.Active == pokemon || board.Bench.Contains(pokemon);
}

public sealed class RetreatHandler(EngineServices services)
{
    public string? Handle(GameState state, GameAction action, List<GameEvent> events)
    {
        if (action.Target is null)
            return "Retreat.TargetRequired";

        var energies = action.EnergiesToDiscard ?? [];
        var error = services.Rules.CanRetreat(state, action.PlayerIndex, energies);
        if (error is not null)
            return error.Code;

        var board = state.Players[action.PlayerIndex];
        var retreating = board.Active!;
        foreach (var energy in energies)
        {
            if (retreating.DetachEnergy(energy))
                board.Discard(energy);
        }

        board.SwitchActive(action.Target);
        retreating.ClearAllStatuses();
        state.RegisterRetreat();
        events.Add(GameEvent.Retreat);
        return null;
    }
}
