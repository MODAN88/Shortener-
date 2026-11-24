using System.Security.Cryptography;

namespace UrlShortener.Api.Application;

public sealed class RandomBase62ShortCodeGenerator : IShortCodeGenerator
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public string Generate()
    {
        Span<char> code = stackalloc char[ShortCodeRules.CodeLength];
        for (var index = 0; index < code.Length; index++)
        {
            code[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(code);
    }
}