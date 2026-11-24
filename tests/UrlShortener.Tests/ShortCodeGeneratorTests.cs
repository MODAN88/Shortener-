using UrlShortener.Api.Application;

namespace UrlShortener.Tests;

public sealed class ShortCodeGeneratorTests
{
    [Fact]
    public void Generate_ReturnsSevenCharacterBase62Code()
    {
        var generator = new RandomBase62ShortCodeGenerator();

        var code = generator.Generate();

        Assert.Equal(7, code.Length);
        Assert.True(ShortCodeRules.IsValid(code));
    }

    [Theory]
    [InlineData("abc123")]
    [InlineData("abcdefghij")]
    [InlineData("abc_123")]
    [InlineData("short")]
    public void IsValid_EnforcesLengthAndAlphabet(string code)
    {
        Assert.Equal(code.Length is >= 6 and <= 10 && code.All(char.IsLetterOrDigit), ShortCodeRules.IsValid(code));
    }
}