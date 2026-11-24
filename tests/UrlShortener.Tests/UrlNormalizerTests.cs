using UrlShortener.Api.Application;

namespace UrlShortener.Tests;

public sealed class UrlNormalizerTests
{
    private readonly UrlNormalizer _normalizer = new();

    [Theory]
    [InlineData("  HTTPS://WWW.Example.COM/  ", "https://www.example.com")]
    [InlineData("HTTPS://EXAMPLE.COM/?x=1", "https://example.com?x=1")]
    [InlineData("https://Example.com/path/", "https://example.com/path/")]
    [InlineData("http://Example.com/a?b=c", "http://example.com/a?b=c")]
    public void TryNormalize_NormalizesRequiredComponents(string input, string expected)
    {
        var result = _normalizer.TryNormalize(input, out var normalized, out var error);

        Assert.True(result, error);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ftp://example.com")]
    [InlineData("/relative/path")]
    [InlineData("https://")]
    public void TryNormalize_RejectsInvalidInput(string? input)
    {
        Assert.False(_normalizer.TryNormalize(input, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void TryNormalize_RejectsUrlLongerThan2048Characters()
    {
        var input = $"https://example.com/{new string('a', 2030)}";

        Assert.False(_normalizer.TryNormalize(input, out _, out var error));
        Assert.Contains("2048", error);
    }
}