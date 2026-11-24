namespace UrlShortener.Api.Application;

public sealed class ShortenerOptions
{
    public const string SectionName = "Shortener";
    public string BaseUrl { get; set; } = "https://sho.rt";
}