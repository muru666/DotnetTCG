using Microsoft.AspNetCore.Mvc.Testing;

namespace DotnetTCG.Api.Tests;

public sealed class TestFactory : WebApplicationFactory<Program>
{
    static TestFactory()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Default",
            Environment.GetEnvironmentVariable("TEST_POSTGRES")
            ?? "Host=localhost;Port=5433;Database=dotnettcp_test;Username=postgres;Password=postgres");
        Environment.SetEnvironmentVariable(
            "Jwt__Key",
            "TEST-ONLY-not-a-real-secret-0123456789abcdef");
    }
}
