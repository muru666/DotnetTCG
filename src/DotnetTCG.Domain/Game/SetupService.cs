using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Common;
using DotnetTCG.Domain.Enums;

namespace DotnetTCG.Domain.Game;

public sealed class SetupService
{
    public const int InitialHandSize = 7;
    public const int PrizeCount = 6;
    public const int SuddenDeathPrizeCount = 1;

    public void InitializeDeal(
        GameState state,
        IReadOnlyList<Card> deck0,
        IReadOnlyList<Card> deck1,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(random);

        state.Players[0].LoadDeck(deck0, shuffle: true, random);
        state.Players[1].LoadDeck(deck1, shuffle: true, random);

        for (var i = 0; i < InitialHandSize; i++)
        {
            state.Players[0].DrawCard();
            state.Players[1].DrawCard();
        }
    }

    public bool NeedsMulligan(PlayerBoard board)
    {
        ArgumentNullException.ThrowIfNull(board);
        return !board.Hand.OfType<PokemonCard>().Any(c => c.IsBasic);
    }

    public void HandleMulligan(GameState state, int playerIndex, Random random)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(random);

        var board = state.Players[playerIndex];
        board.ShuffleHandIntoDeck(random);
        for (var i = 0; i < InitialHandSize; i++)
            board.DrawCard();

        state.RegisterMulligan(playerIndex);
        state.OpponentOf(playerIndex).DrawCard();
    }

    public Error? PlaceActive(GameState state, int playerIndex, PokemonCard card)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(card);

        var board = state.Players[playerIndex];
        if (!card.IsBasic)
            return new Error("Setup.NotBasic", "Only a Basic Pokemon can be placed as Active.");
        if (board.Active is not null)
            return new Error("Setup.ActiveOccupied", "Active spot is already occupied.");
        if (!board.RemoveFromHand(card))
            return new Error("Setup.NotInHand", "Card is not in hand.");

        board.PlaceActive(new PokemonInPlay(card));
        return null;
    }

    public Error? PlaceBench(GameState state, int playerIndex, PokemonCard card)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(card);

        var board = state.Players[playerIndex];
        if (!card.IsBasic)
            return new Error("Setup.NotBasic", "Only a Basic Pokemon can be placed on the bench.");
        if (board.Active is null)
            return new Error("Setup.NoActive", "Active Pokemon must be placed first.");
        if (board.Bench.Count >= PlayerBoard.MaxBenchSize)
            return new Error("Setup.BenchFull", "Bench is full.");
        if (!board.RemoveFromHand(card))
            return new Error("Setup.NotInHand", "Card is not in hand.");

        board.AddToBench(new PokemonInPlay(card));
        return null;
    }

    public Error? ConfirmSetup(GameState state, int playerIndex)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Players[playerIndex].Active is null)
            return new Error("Setup.NoActive", "An Active Pokemon is required to confirm setup.");

        state.ConfirmSetup(playerIndex);
        if (!state.AllSetupConfirmed)
            state.SetCurrentPlayer(1 - playerIndex);
        return null;
    }

    public void FinalizeSetup(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var prizes = state.SuddenDeath ? SuddenDeathPrizeCount : PrizeCount;
        state.Players[0].InitPrizeCards(prizes);
        state.Players[1].InitPrizeCards(prizes);
        state.SetStatus(GameStatus.InProgress);
        state.SetCurrentPlayer(state.StartingPlayerIndex);
        state.ResetTurnCounters();
        state.SetPhase(TurnPhase.Draw);
    }
}
