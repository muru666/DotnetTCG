using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Catalogs;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Engine.Handlers;

public sealed class TrainerHandler(EngineServices services)
{
    public string? Handle(GameState state, GameAction action, List<GameEvent> events)
    {
        if (action.Card is not TrainerCard trainer)
            return "Trainer.CardRequired";

        var board = state.Players[action.PlayerIndex];
        if (!board.HasInHand(trainer))
            return "Trainer.NotInHand";

        var error = services.Rules.CanPlayTrainer(
            state, action.PlayerIndex, trainer, action.Target as PokemonInPlay);
        if (error is not null)
            return error.Code;

        if (trainer.TrainerSubtype == SubType.Tool)
            return AttachTool(state, board, trainer, action.Target as PokemonInPlay);

        if (trainer.TrainerSubtype == SubType.Stadium)
        {
            board.RemoveFromHand(trainer);
            if (state.StadiumCard is not null)
                board.Discard(state.StadiumCard);
            state.SetStadium(trainer);
            state.RegisterStadiumPlayed();
            return null;
        }

        var behavior = TrainerCatalog.Find(trainer.Name);
        if (behavior?.NeedsChoice == true)
            return CreateChoice(state, action, trainer, behavior);

        board.RemoveFromHand(trainer);
        if (behavior?.Effects is not null)
        {
            var target = action.Target as PokemonInPlay ?? board.Active;
            var context = new EffectContext(state, action.PlayerIndex, TargetOf(state, action), target);
            foreach (var spec in behavior.Effects)
                services.Effects.Apply(context, spec);
        }

        board.Discard(trainer);
        if (trainer.TrainerSubtype == SubType.Supporter)
            state.RegisterSupporterPlayed();

        return null;
    }

    private string? AttachTool(GameState state, PlayerBoard board, TrainerCard tool, PokemonInPlay? target)
    {
        if (target is null)
            return "Trainer.ToolNoTarget";
        if (board.Active != target && !board.Bench.Contains(target))
            return "Trainer.ForeignTarget";
        if (!board.RemoveFromHand(tool))
            return "Trainer.NotInHand";
        target.AttachTool(tool);
        var behavior = TrainerCatalog.Find(tool.Name);
        if (behavior?.ToolModifier is not null)
            target.AddModifier(behavior.ToolModifier with { TurnApplied = state.TurnNumber });
        return null;
    }

    private static PokemonInPlay? TargetOf(GameState state, GameAction action)
        => action.Target as PokemonInPlay ?? state.Players[action.PlayerIndex].Active;

    private string? CreateChoice(GameState state, GameAction action, TrainerCard trainer, TrainerBehavior behavior)
    {
        var board = state.Players[action.PlayerIndex];

        // Sin banca libre la elección no podría resolverse nunca (la banca no
        // cambia mientras el pending esté abierto): se rechaza el juego ahora.
        if (behavior.ChoiceKind == "max-revive" && board.Bench.Count >= PlayerBoard.MaxBenchSize)
            return "Choice.BenchFull";

        List<ChoiceOption> options = behavior.ChoiceKind switch
        {
            "evosoda" => MatchingEvolutions(board)
                .Select((c, i) => new ChoiceOption(i.ToString(), c.Name)).ToList(),
            "great-ball" => TopPokemon(board, 7)
                .Select((c, i) => new ChoiceOption(i.ToString(), c.Name)).ToList(),
            "cassius" => FieldOf(board)
                .Select((p, i) => new ChoiceOption(i.ToString(), p.Card.Name)).ToList(),
            "max-revive" => DiscardBasics(board)
                .Select((c, i) => new ChoiceOption(i.ToString(), c.Name)).ToList(),
            _ => [],
        };

        if (options.Count == 0)
            return "Trainer.NoChoice";

        state.SetPendingChoice(new PendingChoice(
            $"trainer:{trainer.Name}:{behavior.ChoiceKind}",
            action.PlayerIndex,
            AbilityName: null,
            $"Choose for {trainer.Name}.",
            options,
            trainer.Name));
        return null;
    }

    public string? ResolveChoice(GameState state, GameAction action)
    {
        var pending = state.PendingChoice;
        if (pending is null || action.ChoiceId != pending.ChoiceId)
            return "Choice.Unknown";
        var selected = action.SelectedOptionIds?.FirstOrDefault();
        if (!int.TryParse(selected, out var index))
            return "Choice.InvalidOption";

        var board = state.Players[action.PlayerIndex];
        var kind = pending.ChoiceId.Split(':').Last();
        string? error = kind switch
        {
            "evosoda" => ApplyEvosoda(state, action.PlayerIndex, index),
            "great-ball" => ApplyGreatBall(state, action.PlayerIndex, index),
            "cassius" => ApplyCassius(state, action.PlayerIndex, index),
            "max-revive" => ApplyMaxRevive(state.Players[action.PlayerIndex], index),
            _ => "Choice.Unknown",
        };

        if (error is not null)
        {
            // Libera la elección: si no, el pending quedaría clavado para siempre
            // (ninguna otra acción pasa por ExecutePending).
            state.SetPendingChoice(null);
            return error;
        }

        var trainer = board.Hand.OfType<TrainerCard>().FirstOrDefault(t => t.Name == pending.Context);
        if (trainer is not null)
        {
            board.RemoveFromHand(trainer);
            board.Discard(trainer);
            if (trainer.TrainerSubtype == SubType.Supporter)
                state.RegisterSupporterPlayed();
        }

        state.SetPendingChoice(null);
        return null;
    }

    private string? ApplyEvosoda(GameState state, int playerIndex, int index)
    {
        var board = state.Players[playerIndex];
        var matches = MatchingEvolutions(board);
        if (index < 0 || index >= matches.Count)
            return "Choice.InvalidOption";
        board.RemoveFromDeck(c => c == matches[index]);
        board.AddToHand(matches[index]);
        board.ShuffleDeck(services.Random);
        return null;
    }

    private string? ApplyGreatBall(GameState state, int playerIndex, int index)
    {
        var board = state.Players[playerIndex];
        var pokemon = TopPokemon(board, 7);
        if (index < 0 || index >= pokemon.Count)
            return "Choice.InvalidOption";
        board.RemoveFromDeck(c => c == pokemon[index]);
        board.AddToHand(pokemon[index]);
        board.ShuffleDeck(services.Random);
        return null;
    }

    private string? ApplyCassius(GameState state, int playerIndex, int index)
    {
        var board = state.Players[playerIndex];
        var field = FieldOf(board);
        if (index < 0 || index >= field.Count)
            return "Choice.InvalidOption";
        var pokemon = field[index];

        foreach (var energy in pokemon.AttachedEnergies.ToList())
        {
            pokemon.DetachEnergy(energy);
            board.AddToHand(energy);
        }

        var tool = pokemon.RemoveTool();
        if (tool is not null)
            board.AddToHand(tool);

        if (board.Active == pokemon)
        {
            board.ClearActive();
            if (board.Bench.Count > 0)
            {
                var replacement = board.Bench[0];
                board.RemoveFromBench(replacement);
                board.PlaceActive(replacement);
            }
        }
        else
        {
            board.RemoveFromBench(pokemon);
        }

        pokemon.HealAll();
        board.AddToHand(pokemon.Card);
        return null;
    }

    private static string? ApplyMaxRevive(PlayerBoard board, int index)
    {
        var basics = DiscardBasics(board);
        if (index < 0 || index >= basics.Count)
            return "Choice.InvalidOption";
        return board.MoveDiscardToBench(basics[index]) ? null : "Choice.BenchFull";
    }

    private static List<PokemonCard> MatchingEvolutions(PlayerBoard board)
    {
        var names = FieldOf(board).Select(p => p.Card.Name).ToHashSet(StringComparer.Ordinal);
        return board.InspectDeck().OfType<PokemonCard>()
            .Where(c => c.EvolvesFrom is not null && names.Contains(c.EvolvesFrom))
            .ToList();
    }

    private static List<PokemonCard> TopPokemon(PlayerBoard board, int count)
        => board.InspectDeck().Take(count).OfType<PokemonCard>().ToList();

    private static List<PokemonInPlay> FieldOf(PlayerBoard board)
    {
        var field = new List<PokemonInPlay>();
        if (board.Active is not null)
            field.Add(board.Active);
        field.AddRange(board.Bench);
        return field;
    }

    private static List<PokemonCard> DiscardBasics(PlayerBoard board)
        => board.DiscardPile.OfType<PokemonCard>().Where(c => c.IsBasic).ToList();
}
