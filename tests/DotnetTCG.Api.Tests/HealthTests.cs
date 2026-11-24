using System.Net;
using DotnetTCG.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotnetTCG.Api.Tests;

public sealed class HealthTests : IClassFixture<TestFactory>
{
    private readonly TestFactory _factory;

    public HealthTests(TestFactory factory)
    {
        // El client se crea DESPUÉS del guard de DB: con la suite sin Docker el
        // host ni siquiera levanta (arranca migrando).
        _factory = factory;
    }

    [SkippableFact]
    public async Task Health_ReturnsOk()
    {
        if (!await DbAsync())
            return;
        var response = await _factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<bool> DbAsync()
    {
        try
        {
            using var scope = _factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
            return true;
        }
        catch
        {
            Skip.If(true, "Postgres de test no disponible (Docker).");
            return false;
        }
    }
}
