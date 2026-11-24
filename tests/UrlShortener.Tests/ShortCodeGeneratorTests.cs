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
    [InlineData("abc١23")]
    [InlineData("ABCdef")]
    public void IsValid_EnforcesLengthAndAlphabet(string code)
    {
        var expected = code.Length is >= 6 and <= 10 &&
                       code.All(character => character is >= 'a' and <= 'z'
                                           or >= 'A' and <= 'Z'
                                           or >= '0' and <= '9');

        Assert.Equal(expected, ShortCodeRules.IsValid(code));
    }
}