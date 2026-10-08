using DotnetTCG.Application.Auth;
using DotnetTCG.Application.Cards;
using DotnetTCG.Application.Decks;
using DotnetTCG.Application.Users;
using DotnetTCG.Domain.Game;
using Microsoft.Extensions.DependencyInjection;

namespace DotnetTCG.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<RuleValidator>();
        services.AddScoped<CardService>();
        services.AddScoped<DeckService>();
        services.AddScoped<UserService>();
        services.AddScoped<RecoveryService>();
        return services;
    }
}
