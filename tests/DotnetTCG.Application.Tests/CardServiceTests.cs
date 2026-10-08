using DotnetTCG.Application.Cards;

namespace DotnetTCG.Application.Tests;

public sealed class CardServiceTests
{
    [Fact]
    public async Task GetById_Missing_NotFound()
    {
        var service = new CardService(new FakeCardRepository(), new FakePokeApiClient([]));

        var result = await service.GetByIdAsync("xy1-1", CancellationToken.None);

        Assert.Equal("Card.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task Search_ClampsPageSize()
    {
        var service = new CardService(new FakeCardRepository(TestCardFactory.Catalog()), new FakePokeApiClient([]));

        var result = await service.SearchAsync(null, null, -1, 500, CancellationToken.None);

        Assert.Equal(0, result.Page);
        Assert.Equal(CardService.MaxPageSize, result.PageSize);
    }

    [Fact]
    public async Task ImportSet_UpsertsFetched()
    {
        var repo = new FakeCardRepository();
        var pokemon = TestCardFactory.Pokemon("xy1-1", "Venusaur");
        var service = new CardService(repo, new FakePokeApiClient([pokemon]));

        var result = await service.ImportSetAsync("xy1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        Assert.Equal(1, repo.UpsertedCount);
    }
}
