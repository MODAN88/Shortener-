using Microsoft.EntityFrameworkCore;
using UrlShortener.Api.Application;
using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Infrastructure;

public sealed class UrlShortenerService(
    ShortenerDbContext db,
    IShortCodeGenerator codeGenerator,
    ILogger<UrlShortenerService> logger) : IUrlShortenerService
{
    private const int MaximumInsertAttempts = 8;

    public async Task<CreateMappingResult> CreateAsync(string normalizedUrl, CancellationToken cancellationToken)
    {
        var existing = await db.UrlMappings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.NormalizedLongUrl == normalizedUrl, cancellationToken);
        if (existing is not null) return new CreateMappingResult(existing, false);

        for (var attempt = 1; attempt <= MaximumInsertAttempts; attempt++)
        {
            var mapping = new UrlMapping
            {
                ShortCode = codeGenerator.Generate(),
                NormalizedLongUrl = normalizedUrl,
                LongUrl = normalizedUrl,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            db.UrlMappings.Add(mapping);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Created short URL {ShortCode} for normalized URL {NormalizedUrl}",
                    mapping.ShortCode, normalizedUrl);
                return new CreateMappingResult(mapping, true);
            }
            catch (DbUpdateException) when (attempt < MaximumInsertAttempts)
            {
                db.Entry(mapping).State = EntityState.Detached;
                existing = await db.UrlMappings.AsNoTracking()
                    .SingleOrDefaultAsync(item => item.NormalizedLongUrl == normalizedUrl, cancellationToken);
                if (existing is not null) return new CreateMappingResult(existing, false);

                logger.LogWarning("Uniqueness collision on attempt {Attempt}; retrying", attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(10 * attempt), cancellationToken);
            }
        }

        throw new InvalidOperationException("Could not allocate a unique short code after several attempts.");
    }

    public Task<UrlMapping?> FindByCodeAsync(string shortCode, CancellationToken cancellationToken) =>
        db.UrlMappings.AsNoTracking().SingleOrDefaultAsync(item => item.ShortCode == shortCode, cancellationToken);
}