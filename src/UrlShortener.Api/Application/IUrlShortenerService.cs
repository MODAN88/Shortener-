using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Application;

public interface IUrlShortenerService
{
    Task<CreateMappingResult> CreateAsync(string normalizedUrl, CancellationToken cancellationToken);
    Task<UrlMapping?> FindByCodeAsync(string shortCode, CancellationToken cancellationToken);
}

public sealed record CreateMappingResult(UrlMapping Mapping, bool Created);