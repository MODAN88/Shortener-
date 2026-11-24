namespace UrlShortener.Api.Application;

public sealed class UrlNormalizer : IUrlNormalizer
{
    public const int MaximumLength = 2048;

    public bool TryNormalize(string? input, out string normalizedUrl, out string error)
    {
        normalizedUrl = string.Empty;
        if (input is null || string.IsNullOrWhiteSpace(input))
        {
            error = "The longUrl field is required and cannot be empty.";
            return false;
        }

        if (input.Length > MaximumLength)
        {
            error = $"The URL must not exceed {MaximumLength} characters.";
            return false;
        }

        var trimmed = input.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            error = "The URL must be an absolute HTTP or HTTPS URL with a valid host.";
            return false;
        }

        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.Host.ToLowerInvariant()
        };
        var normalizedUri = builder.Uri;
        normalizedUrl = normalizedUri.AbsolutePath == "/"
            ? normalizedUri.GetLeftPart(UriPartial.Authority) + normalizedUri.Query + normalizedUri.Fragment
            : normalizedUri.AbsoluteUri;
        error = string.Empty;
        return true;
    }
}