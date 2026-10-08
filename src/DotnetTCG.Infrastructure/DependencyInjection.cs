using DotnetTCG.Application.Common.Interfaces;
using DotnetTCG.Infrastructure.Clock;
using DotnetTCG.Infrastructure.External;
using DotnetTCG.Infrastructure.Persistence;
using DotnetTCG.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotnetTCG.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();

        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Missing connection string 'Default'. Set ConnectionStrings__Default env var.");
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<ICardRepository, EfCardRepository>();
        services.AddScoped<IDeckRepository, EfDeckRepository>();

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

        return services;
    }
}
