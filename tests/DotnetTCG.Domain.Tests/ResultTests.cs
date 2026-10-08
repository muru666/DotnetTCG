using DotnetTCG.Domain.Common;

namespace DotnetTCG.Domain.Tests;

public sealed class ResultTests
{
    [Fact]
    public void Success_HoldsValue()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_HoldsError()
    {
        var error = new Error("Deck.Invalid", "Deck must have exactly 60 cards.");

        var result = Result<int>.Failure(error);

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Equal(error, result.Error);
    }
}
