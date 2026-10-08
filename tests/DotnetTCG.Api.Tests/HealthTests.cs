using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DotnetTCG.Api.Tests;

public sealed class HealthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthTests(WebApplicationFactory<Program> factory)
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Default",
            "Host=localhost;Port=5432;Database=dotnettcp_test;Username=postgres;Password=postgres");
        Environment.SetEnvironmentVariable(
            "Jwt__Key",
            "TEST-ONLY-not-a-real-secret-0123456789abcdef");

        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
