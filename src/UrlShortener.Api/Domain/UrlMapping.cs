namespace UrlShortener.Api.Domain;

public sealed class UrlMapping
{
    public int Id { get; set; }
    public required string ShortCode { get; set; }
    public required string NormalizedLongUrl { get; set; }
    public required string LongUrl { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}