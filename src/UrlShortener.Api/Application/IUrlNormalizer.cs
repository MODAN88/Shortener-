namespace UrlShortener.Api.Application;

public interface IUrlNormalizer
{
    bool TryNormalize(string? input, out string normalizedUrl, out string error);
}