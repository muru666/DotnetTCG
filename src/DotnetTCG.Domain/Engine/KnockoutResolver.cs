using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Engine;

public sealed class KnockoutResolver
{
    public void Resolve(GameState state, List<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(events);

        for (var player = 0; player < 2; player++)
            ResolvePlayerField(state, player, events);

        for (var player = 0; player < 2; player++)
            PromoteFromBench(state.Players[player]);
    }

    public void ApplyDestinyBurst(GameState state, int attackerOwner, PokemonInPlay attacker, List<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(events);

        if (!attacker.KnockedOut)
            attacker.AddDamage(attacker.HpRemaining);
        ResolvePlayerField(state, attackerOwner, events);
        PromoteFromBench(state.Players[attackerOwner]);
    }

    private static void ResolvePlayerField(GameState state, int owner, List<GameEvent> events)
    {
        var board = state.Players[owner];
        var knockedOut = FieldOf(board).Where(p => p.KnockedOut).ToList();

        foreach (var pokemon in knockedOut)
        {
            foreach (var energy in pokemon.AttachedEnergies.ToList())
            {
                pokemon.DetachEnergy(energy);
                board.Discard(energy);
            }

            var tool = pokemon.RemoveTool();
            if (tool is not null)
                board.Discard(tool);

            board.Discard(pokemon.Card);
            if (board.Active == pokemon)
                board.ClearActive();
            else
                board.RemoveFromBench(pokemon);

            events.Add(GameEvent.Knockout);

            var prizes = pokemon.Card.IsEx ? 2 : 1;
            var taker = state.Players[1 - owner];
            for (var i = 0; i < prizes; i++)
            {
                if (taker.TakePrize().IsSuccess)
                    events.Add(GameEvent.PrizeTaken);
            }
        }
    }

    private static void PromoteFromBench(PlayerBoard board)
    {
        if (board.Active is null && board.Bench.Count > 0)
        {
            var replacement = board.Bench[0];
            board.RemoveFromBench(replacement);
            board.PlaceActive(replacement);
            replacement.JustPlayed = false;
        }
    }

    private static List<PokemonInPlay> FieldOf(PlayerBoard board)
    {
        var field = new List<PokemonInPlay>();
        if (board.Active is not null)
            field.Add(board.Active);
        field.AddRange(board.Bench);
        return field;
    }
}
