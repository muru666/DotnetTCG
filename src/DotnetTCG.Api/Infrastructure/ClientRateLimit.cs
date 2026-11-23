namespace DotnetTCG.Api.Infrastructure;

/// <summary>
/// Lógica de partición del rate limiting. Vive fuera de Program.cs para poder
/// testearla: la política "auth" debe partir POR CLIENTE, nunca global
/// (un bucket compartido permitía bloquear el login de todos los usuarios
/// con 20 req/min — ver docs/AUDIT-2026-11.md, BUG-008).
/// </summary>
public static class ClientRateLimit
{
    public static string PartitionKey(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
