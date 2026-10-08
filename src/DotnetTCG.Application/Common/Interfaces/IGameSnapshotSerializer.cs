using DotnetTCG.Domain.Cards;
using DotnetTCG.Domain.Game;

namespace DotnetTCG.Application.Common.Interfaces;

public interface IGameSnapshotSerializer
{
    string Serialize(GameState state);

    GameState Deserialize(string snapshot, IReadOnlyDictionary<string, Card> cardsById);

    IReadOnlyList<string> CollectCardIds(string snapshot);
}
