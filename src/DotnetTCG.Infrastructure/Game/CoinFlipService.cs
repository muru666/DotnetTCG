using System.Security.Cryptography;

namespace DotnetTCG.Infrastructure.Game;

public sealed class CoinFlipService : DotnetTCG.Domain.Game.ICoinFlipService
{
    public bool FlipHeads() => RandomNumberGenerator.GetInt32(0, 2) == 0;
}
