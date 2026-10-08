using DotnetTCG.Domain.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace DotnetTCG.Infrastructure;

public static class GameEngineFactory
{
    public static GameEngine Create(IServiceProvider services)
    {
        var coinFlip = services.GetRequiredService<DotnetTCG.Domain.Game.ICoinFlipService>();
        return new GameEngine(GameEngine.CreateDefault(Random.Shared, coinFlip));
    }
}
