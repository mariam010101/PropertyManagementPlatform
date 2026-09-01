using PMP.Shared.Common;

namespace PMP.Tests;

public class ResultTests
{
    [Fact]
    public void Ok_ContainsValue()
    {
        var result = Result.Ok(42);
        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Fail_SetsError()
    {
        var result = Result.Fail("boom");
        Assert.False(result.Succeeded);
        Assert.Equal("boom", result.Error);
    }

    [Fact]
    public void GenericOk_ContainsData()
    {
        var result = Result.Ok("hello");
        Assert.True(result.Succeeded);
        Assert.Equal("hello", result.Data);
    }
}
