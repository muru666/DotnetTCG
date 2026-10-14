using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DotnetTCG.Api.Tests;

public sealed class HealthTests : IClassFixture<TestFactory>
{
    private readonly HttpClient _client;

    public HealthTests(TestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
