using DotnetTCG.Domain.Abilities;
using DotnetTCG.Domain.Attacks;
using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Engine.Handlers;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Engine;

public sealed class GameEngine
{
    private readonly EngineServices _services;
    private readonly KnockoutResolver _knockouts;
    private readonly PlayPokemonHandler _playPokemon;
    private readonly EvolveHandler _evolve;
    private readonly AttachEnergyHandler _attachEnergy;
    private readonly TrainerHandler _trainer;
    private readonly RetreatHandler _retreat;
    private readonly AttackHandler _attack;
    private readonly AbilityHandler _ability;
    private readonly TurnControlHandler _turns;

    public GameEngine(EngineServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        _knockouts = new KnockoutResolver();
        _playPokemon = new PlayPokemonHandler(services);
        _evolve = new EvolveHandler(services);
        _attachEnergy = new AttachEnergyHandler(services);
        _trainer = new TrainerHandler(services);
        _retreat = new RetreatHandler(services);
        _attack = new AttackHandler(services);
        _ability = new AbilityHandler(services);
        _turns = new TurnControlHandler(services, _knockouts);
    }

    public static EngineServices CreateDefault(Random random, ICoinFlipService coinFlip)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(coinFlip);
        var statuses = new StatusEffectManager();
        var effects = new EffectEngine(statuses, coinFlip, random);
        return new EngineServices(
            new RuleValidator(),
            new TurnManager(),
            new DamageCalculator(),
            statuses,
            new VictoryConditionChecker(),
            new SetupService(),
            new AttackRegistry(),
            new AbilityRegistry(),
            effects,
            new AttackPipeline(new DamageCalculator(), effects, statuses, coinFlip, random),
            coinFlip,
            random);
    }

    public GameState NewGame(
        IReadOnlyList<Card> deck0,
        IReadOnlyList<Card> deck1,
        int startingPlayerIndex,
        bool suddenDeath = false)
    {
        var errors0 = _services.Rules.ValidateDeck(deck0);
        var errors1 = _services.Rules.ValidateDeck(deck1);
        var errors = errors0.Concat(errors1).ToList();
        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Invalid decks: " + string.Join("; ", errors.Select(e => e.Code)));

        var state = new GameState(new PlayerBoard(), new PlayerBoard(), startingPlayerIndex, suddenDeath);
        _services.Setup.InitializeDeal(state, deck0, deck1, _services.Random);
        return state;
    }

    public void PerformMulligan(GameState state, int playerIndex)
    {
        _services.Setup.HandleMulligan(state, playerIndex, _services.Random);
    }

    public GameActionResult Execute(GameState state, GameAction action)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(action);

        var events = new List<GameEvent>();

        if (state.Status == GameStatus.Finished)
            return new GameActionResult(false, "Game.Finished", null, events);

        if (state.Status == GameStatus.Setup)
            return ExecuteSetup(state, action, events);

        if (state.PendingChoice is not null)
            return ExecutePending(state, action, events);

        if (action.PlayerIndex != state.CurrentPlayerIndex
            && action.Type is not (GameActionType.PassTurn or GameActionType.Concede))
            return new GameActionResult(false, "Turn.NotYourTurn", null, events);

        var ensure = EnsurePhase(state, action.Type, events);
        if (ensure is not null)
            return ensure;

        string? error = action.Type switch
        {
            GameActionType.PlayCard when action.Card is PokemonCard => _playPokemon.Handle(state, action),
            GameActionType.PlayCard when action.Card is EnergyCard => _attachEnergy.Handle(state, action),
            GameActionType.PlayCard when action.Card is TrainerCard => _trainer.Handle(state, action, events),
            GameActionType.PlayCard => "Play.UnknownCard",
            GameActionType.Evolve => HandleEvolve(state, action),
            GameActionType.AttachEnergy => _attachEnergy.Handle(state, action),
            GameActionType.PlaySupporter or GameActionType.PlayStadium or GameActionType.UseAbility
                when action.Card is TrainerCard => _trainer.Handle(state, action, events),
            GameActionType.PlaySupporter or GameActionType.PlayStadium => "Trainer.CardRequired",
            GameActionType.Retreat => _retreat.Handle(state, action, events),
            GameActionType.UseAbility or GameActionType.ActivateAbility
                => HandleAbility(state, action, events),
            GameActionType.Attack => HandleAttack(state, action, events),
            GameActionType.PassTurn => _turns.PassTurn(state, action.PlayerIndex),
            GameActionType.Concede => _turns.Concede(state, action.PlayerIndex),
            GameActionType.DrawCard => HandleManualDraw(state, action),
            _ => "Action.Unknown",
        };

        if (error is not null)
            return new GameActionResult(false, error, null, events);

        if (state.PendingChoice is not null)
            return new GameActionResult(true, null, state.PendingChoice, events);

        CheckVictory(state, events);
        return new GameActionResult(true, null, null, events);
    }

    private GameActionResult ExecuteSetup(GameState state, GameAction action, List<GameEvent> events)
    {
        if (action.PlayerIndex != state.CurrentPlayerIndex)
            return new GameActionResult(false, "Turn.NotYourTurn", null, events);

        string? error = action.Type switch
        {
            GameActionType.PlayCard when action.Card is PokemonCard card
                && state.Players[action.PlayerIndex].Active is null
                => _services.Setup.PlaceActive(state, action.PlayerIndex, card)?.Code,
            GameActionType.PlayCard when action.Card is PokemonCard card
                => _services.Setup.PlaceBench(state, action.PlayerIndex, card)?.Code,
            GameActionType.PassTurn => _services.Setup.ConfirmSetup(state, action.PlayerIndex)?.Code,
            _ => "Setup.InvalidAction",
        };

        if (error is not null)
            return new GameActionResult(false, error, null, events);

        if (state.AllSetupConfirmed)
        {
            _services.Setup.FinalizeSetup(state);
            events.Add(GameEvent.SetupComplete);
            events.Add(GameEvent.GameStart);
        }

        return new GameActionResult(true, null, null, events);
    }

    private GameActionResult ExecutePending(GameState state, GameAction action, List<GameEvent> events)
    {
        var pending = state.PendingChoice!;
        if (action.ChoiceId != pending.ChoiceId)
            return new GameActionResult(false, "Choice.Pending", pending, events);

        string? error;
        if (pending.ChoiceId.StartsWith("attack:", StringComparison.Ordinal))
            error = _attack.Resume(state, action, events);
        else if (pending.ChoiceId.StartsWith("ability:", StringComparison.Ordinal))
            error = _ability.Resume(state, action, events);
        else if (pending.ChoiceId.StartsWith("trainer:", StringComparison.Ordinal))
            error = _trainer.ResolveChoice(state, action);
        else
            error = "Choice.Unknown";

        if (error is not null)
            return new GameActionResult(false, error, state.PendingChoice, events);

        if (state.PendingChoice is not null)
            return new GameActionResult(true, null, state.PendingChoice, events);

        if (state.PendingAttack is not null)
        {
            _knockouts.Resolve(state, events);
            CheckVictory(state, events);
            if (state.Status != GameStatus.Finished)
                _turns.EndTurn(state, events);
            state.SetPendingAttack(null);
        }

        CheckVictory(state, events);
        return new GameActionResult(true, null, null, events);
    }

    private GameActionResult? EnsurePhase(GameState state, GameActionType type, List<GameEvent> events)
    {
        var needsMain = type is GameActionType.PlayCard or GameActionType.Evolve
            or GameActionType.AttachEnergy or GameActionType.PlaySupporter or GameActionType.PlayStadium
            or GameActionType.Retreat or GameActionType.UseAbility or GameActionType.ActivateAbility
            or GameActionType.Attack;

        if (!needsMain)
            return null;

        if (state.Phase == TurnPhase.Draw)
        {
            var draw = _services.Turns.ProcessAutoDraw(state);
            if (draw.IsFailure)
            {
                state.Finish(1 - state.CurrentPlayerIndex);
                events.Add(GameEvent.GameOver);
                return new GameActionResult(false, "Game.DeckOut", null, events);
            }
        }

        if (type == GameActionType.Attack && state.Phase == TurnPhase.Main)
        {
            if (!_services.Turns.CanGoToAttack(state))
                return new GameActionResult(false, "Attack.FirstTurn", null, events);
            _services.Turns.GoToAttack(state);
        }
        else if (type != GameActionType.Attack && state.Phase == TurnPhase.Attack)
        {
            return new GameActionResult(false, "Turn.AlreadyAttacked", null, events);
        }

        return null;
    }

    private string? HandleEvolve(GameState state, GameAction action)
    {
        var error = _evolve.Handle(state, action);
        if (error is not null)
            return error;
        if (action.Card is PokemonCard { Subtypes: var subtypes }
            && subtypes.Contains(SubType.Mega))
            _turns.EndTurn(state, []);
        return null;
    }

    private string? HandleAbility(GameState state, GameAction action, List<GameEvent> events)
    {
        var error = _ability.Handle(state, action, events);
        if (error is not null)
            return error;
        _knockouts.Resolve(state, events);
        return null;
    }

    private string? HandleAttack(GameState state, GameAction action, List<GameEvent> events)
    {
        var attackerBefore = state.Players[action.PlayerIndex].Active;
        var defenderBefore = state.OpponentOf(action.PlayerIndex).Active;

        var error = _attack.Handle(state, action, events);
        if (error is not null)
            return error;
        if (state.PendingChoice is not null)
            return null;

        if (defenderBefore is not null && defenderBefore.KnockedOut
            && defenderBefore.Card.Abilities.Any(a => a.Name == "Destiny Burst")
            && attackerBefore is not null)
        {
            _knockouts.ApplyDestinyBurst(state, action.PlayerIndex, attackerBefore, events);
        }

        _knockouts.Resolve(state, events);
        CheckVictory(state, events);
        if (state.Status != GameStatus.Finished)
            _turns.EndTurn(state, events);
        return null;
    }

    private string? HandleManualDraw(GameState state, GameAction action)
    {
        if (state.Phase != TurnPhase.Draw)
            return "Draw.NotDrawPhase";
        var draw = _services.Turns.ProcessAutoDraw(state);
        return draw.IsFailure ? "Game.DeckOut" : null;
    }

    private void CheckVictory(GameState state, List<GameEvent> events)
    {
        if (state.Status == GameStatus.Finished)
            return;
        var result = _services.Victories.Check(state);
        if (result.RequiresSuddenDeath)
        {
            StartSuddenDeath(state);
            events.Add(GameEvent.GameStart);
        }
        else if (result.GameOver)
        {
            state.Finish(result.WinnerPlayerIndex);
            events.Add(GameEvent.GameOver);
        }
    }

    private void StartSuddenDeath(GameState state)
    {
        foreach (var board in state.Players)
        {
            var cards = board.CollectAllCards();
            board.Reset();
            board.LoadDeck(cards, shuffle: true, _services.Random);
            for (var i = 0; i < SetupService.InitialHandSize && board.DrawCard().IsSuccess; i++)
            {
            }
        }

        state.MarkSuddenDeath();
        state.ResetSetup();
        state.SetStatus(GameStatus.Setup);
        state.SetCurrentPlayer(state.StartingPlayerIndex);
        state.SetPhase(TurnPhase.Draw);
    }
}
