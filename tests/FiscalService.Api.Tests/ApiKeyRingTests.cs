using FiscalService.Api.Configuration;
using Xunit;

namespace FiscalService.Api.Tests;

public class ApiKeyRingTests
{
    [Theory]
    [InlineData("abc", "abc", true)]
    [InlineData("a,b", "b", true)]
    [InlineData("a|b", "a", true)]
    [InlineData("nova; antiga", "antiga", true)]
    [InlineData("x", "y", false)]
    [InlineData("", "x", false)]
    [InlineData("   ", "x", false)]
    public void Matches(string configured, string provided, bool expected) =>
        Assert.Equal(expected, ApiKeyRing.Matches(configured, provided));
}
