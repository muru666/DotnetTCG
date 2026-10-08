using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Engine;
using DotnetTCG.Domain.Enums;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Domain.Tests;

public sealed class BotVsBotTests
{
    private static List<Card> BotDeck(string prefix)
    {
        var cards = new List<Card>();
        for (var i = 0; i < 10; i++)
        {
            var attack = new Attack("Tackle", string.Empty, "30", [PokemonType.Colorless]);
            cards.Add(new PokemonCard(
                $"{prefix}-{i}", $"Bot{prefix}{i}", [SubType.Basic], [], "xy1", string.Empty,
                Hp: 60, RetreatCost: 1, EvolvesFrom: null, PokemonType.Colorless,
                [attack], [], [], []));
        }

        for (var i = 0; i < 50; i++)
            cards.Add(TestCards.BasicEnergy($"{prefix}-e{i}", PokemonType.Colorless));
        return cards;
    }

    private static GameEngine NewEngine()
        => new(GameEngine.CreateDefault(new Random(123), new SeededCoinFlip(
            Enumerable.Repeat(true, 5000).ToArray())));

    private static void DoSetup(GameEngine engine, GameState state, int player)
    {
        var board = state.Players[player];
        var basics = board.Hand.OfType<PokemonCard>().Where(c => c.IsBasic).Take(3).ToList();
        Assert.NotEmpty(basics);

        foreach (var basic in basics)
        {
            var result = engine.Execute(state, new GameAction(GameActionType.PlayCard, player, Card: basic));
            Assert.True(result.Success, result.Error);
        }

        var confirm = engine.Execute(state, new GameAction(GameActionType.PassTurn, player));
        Assert.True(confirm.Success, confirm.Error);
    }

    private static void ResolvePending(GameEngine engine, GameState state, GameActionResult pending)
    {
        var choice = pending.PendingChoice!;
        var player = choice.PlayerIndex;
        string option = choice.Options[0].OptionId;
        if (choice.ChoiceId.Contains("optional-discard"))
        {
            var attacker = state.Players[player].Active!;
            option = attacker.TotalEnergyCount > 0 ? "yes" : "no";
        }

        var result = engine.Execute(state, new GameAction(
            GameActionType.Attack, player,
            ChoiceId: choice.ChoiceId, SelectedOptionIds: [option]));
        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public void FullGame_BotVsBot_ReachesFinished()
    {
        var engine = NewEngine();
        var state = engine.NewGame(BotDeck("a"), BotDeck("b"), startingPlayerIndex: 0);

        for (var p = 0; p < 2; p++)
        {
            while (engine is not null && state.Status == GameStatus.Setup
                && state.Players[p].Hand.Count > 0
                && !state.Players[p].Hand.OfType<PokemonCard>().Any(c => c.IsBasic))
                engine.PerformMulligan(state, p);
        }

        var turns = 0;
        while (state.Status != GameStatus.Finished && turns < 500)
        {
            turns++;
            var player = state.CurrentPlayerIndex;

            if (state.Status == GameStatus.Setup)
            {
                DoSetup(engine, state, player);
                continue;
            }

            if (state.PendingChoice is not null)
            {
                ResolvePending(engine, state,
                    new GameActionResult(true, null, state.PendingChoice));
                continue;
            }

            var board = state.Players[player];
            var active = board.Active!;

            var energy = board.Hand.OfType<EnergyCard>().FirstOrDefault();
            if (energy is not null)
            {
                var attach = engine.Execute(state,
                    new GameAction(GameActionType.AttachEnergy, player, Card: energy, Target: active));
                Assert.True(attach.Success, attach.Error);
                if (attach.PendingChoice is not null)
                {
                    ResolvePending(engine, state, attach);
                    continue;
                }
            }

            var attack = active.Card.Attacks.FirstOrDefault();
            GameActionResult acted = attack is null
                ? engine.Execute(state, new GameAction(GameActionType.PassTurn, player))
                : engine.Execute(state, new GameAction(GameActionType.Attack, player, Attack: attack));

            if (!acted.Success && acted.Error is "Attack.FirstTurn" or "Attack.EnergyShort")
                acted = engine.Execute(state, new GameAction(GameActionType.PassTurn, player));

            Assert.True(acted.Success, acted.Error);
            if (acted.PendingChoice is not null)
                ResolvePending(engine, state, acted);
        }

        Assert.Equal(GameStatus.Finished, state.Status);
        Assert.NotNull(state.WinnerPlayerIndex);
    }

    [Fact]
    public void NewGame_InvalidDeck_Throws()
    {
        var engine = NewEngine();

        Assert.Throws<InvalidOperationException>(() =>
            engine.NewGame(BotDeck("a").Take(59).ToList(), BotDeck("b"), startingPlayerIndex: 0));
    }
}
