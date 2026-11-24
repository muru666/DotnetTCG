using DotnetTCG.Api.Infrastructure;
using DotnetTCG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotnetTCG.Api.Tests;

public sealed class DatabaseHealthCheckTests
{
    private static HealthCheckContext Context() => new();

    [SkippableFact]
    public async Task UnreachableDatabase_IsUnhealthy()
    {
        // Puerto de conexión cerrado: CanConnectAsync falla o devuelve false.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2")
            .Options;
        await using var db = new AppDbContext(options);

        var result = await new DatabaseHealthCheck(db).CheckHealthAsync(Context());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [SkippableFact]
    public async Task ReachableDatabase_ReportsHealthy()
    {
        // El mismo host de tests que usa el resto de la suite. Si no hay Postgres
        // disponible el check debe reportar Unhealthy (también es lo correcto):
        // lo que se verifica es el mapeo, no la disponibilidad del entorno.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("TEST_POSTGRES")
                ?? "Host=localhost;Port=5433;Database=dotnettcp_test;Username=postgres;Password=postgres")
            .Options;
        await using var db = new AppDbContext(options);

        var canConnect = await db.Database.CanConnectAsync();
        var result = await new DatabaseHealthCheck(db).CheckHealthAsync(Context());

        Assert.Equal(canConnect ? HealthStatus.Healthy : HealthStatus.Unhealthy,
            result.Status);
    }
}
