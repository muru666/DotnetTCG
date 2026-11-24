using System.Net;
using DotnetTCG.Api.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace DotnetTCG.Api.Tests;

public sealed class ClientRateLimitTests
{
    private static HttpContext ContextWithIp(string? ip)
    {
        var context = new DefaultHttpContext();
        if (ip is not null)
            context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return context;
    }

    [SkippableFact]
    public void PartitionKey_DifferentClients_GetDifferentBuckets()
    {
        // Regresión BUG-008: con la clave constante (bucket global), cualquier
        // cliente anónimo agotando los 20 permisos bloqueaba el login de todos.
        var a = ClientRateLimit.PartitionKey(ContextWithIp("203.0.113.10"));
        var b = ClientRateLimit.PartitionKey(ContextWithIp("203.0.113.20"));

        Assert.NotEqual(a, b);
    }

    [SkippableFact]
    public void PartitionKey_SameClient_SameBucket()
    {
        var first = ClientRateLimit.PartitionKey(ContextWithIp("203.0.113.10"));
        var second = ClientRateLimit.PartitionKey(ContextWithIp("203.0.113.10"));

        Assert.Equal(first, second);
    }

    [SkippableFact]
    public void PartitionKey_WithoutRemoteIp_FallsBackToStableKey()
    {
        // TestServer no setea RemoteIpAddress: la clave debe ser estable y no nula.
        Assert.Equal("unknown", ClientRateLimit.PartitionKey(ContextWithIp(null)));
    }
}
