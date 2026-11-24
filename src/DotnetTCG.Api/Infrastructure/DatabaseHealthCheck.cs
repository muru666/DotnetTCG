using DotnetTCG.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotnetTCG.Api.Infrastructure;

/// <summary>
/// /health debe mentir en positivo: antes devolvía 200 siempre (sin checks) y un
/// despliegue con la DB caída o vacía pasaba por "sano" mientras todo respondía
/// 500. Ver docs/AUDIT-2026-11.md BUG-011.
/// </summary>
public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Database reachable.")
                : HealthCheckResult.Unhealthy("Database unreachable.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Database check failed.", ex);
        }
    }
}
