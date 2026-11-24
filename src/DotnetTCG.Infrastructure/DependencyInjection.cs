using DotnetTCG.Application.Auth;
using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Domain.Engine;
using DotnetTCG.Domain.Game;
using DotnetTCG.Infrastructure.Auth;
using DotnetTCG.Infrastructure.Clock;
using DotnetTCG.Infrastructure.Email;
using DotnetTCG.Infrastructure.External;
using DotnetTCG.Infrastructure.Persistence.Snapshots;
using DotnetTCG.Infrastructure.Persistence;
using DotnetTCG.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotnetTCG.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // TimeProvider como primitiva única: IClock lo adapta. TryAdd para que
        // un composition root o un test pueda reemplazarlo (p. ej. FakeTimeProvider).
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IClock>(sp => new SystemClock(sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<Domain.Game.ICoinFlipService, Game.CoinFlipService>();

        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Missing connection string 'Default'. Set ConnectionStrings__Default env var.");
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<ICardRepository, EfCardRepository>();
        services.AddScoped<IGameRepository, EfGameRepository>();
        services.AddScoped<IGameLogRepository, EfGameLogRepository>();
        services.AddSingleton<IGameSnapshotSerializer, GameSnapshotMapper>();
        services.AddSingleton(GameEngineFactory.Create);
        services.AddScoped<IDeckRepository, EfDeckRepository>();
        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IRecoveryTokenRepository, EfRecoveryTokenRepository>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        services.Configure<PokeApiOptions>(configuration.GetSection(PokeApiOptions.SectionName));
        services.AddHttpClient<PokeApiClient>(client =>
        {
            var baseUrl = configuration.GetSection(PokeApiOptions.SectionName).Get<PokeApiOptions>()?.BaseUrl
                ?? "https://api.pokemontcg.io/v2/";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IPokeApiClient>(sp =>
            sp.GetRequiredService<PokeApiClient>());
        services.AddScoped<CardSeedService>();

        return services;
    }
}
