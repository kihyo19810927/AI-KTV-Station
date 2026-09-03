using Station.Application.Common;

namespace Station.Core.Tests;

public sealed class ResultTests
{
    [Fact]
    public void Success_exposes_value() => Assert.Equal(42, Result<int>.Success(42).Value);

    [Fact]
    public void Failure_exposes_error_and_rejects_value_access()
    {
        var result = Result<int>.Failure(new Error("song.offline", "Song is unavailable."));
        Assert.True(result.IsFailure);
        Assert.Equal("song.offline", result.Error.Code);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
