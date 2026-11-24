namespace UrlShortener.Api.Application;

public static class ShortCodeRules
{
    public const int CodeLength = 7;

    public static bool IsValid(string? code) =>
        code is { Length: >= 6 and <= 10 } &&
        code.All(character => character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9');
}