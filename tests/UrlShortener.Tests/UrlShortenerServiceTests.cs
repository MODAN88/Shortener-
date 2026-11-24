using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UrlShortener.Api.Application;
using UrlShortener.Api.Infrastructure;

namespace UrlShortener.Tests;

public sealed class UrlShortenerServiceTests
{
    [Fact]
    public async Task CreateAsync_ExistingNormalizedUrlReturnsExistingMapping()
    {
        await using var db = await CreateDbContextAsync();
        var generator = new QueueShortCodeGenerator("Abc1234");
        var service = new UrlShortenerService(db, generator,
            NullLogger<UrlShortenerService>.Instance);

        var first = await service.CreateAsync("https://example.com/path", CancellationToken.None);
        var second = await service.CreateAsync("https://example.com/path", CancellationToken.None);

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.Mapping.ShortCode, second.Mapping.ShortCode);
        Assert.Equal(1, await db.UrlMappings.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_CodeCollisionRetriesAndStoresBothMappings()
    {
        await using var db = await CreateDbContextAsync();
        var generator = new QueueShortCodeGenerator("Abc1234", "XyZ9876");
        var service = new UrlShortenerService(db, generator,
            NullLogger<UrlShortenerService>.Instance);

        var first = await service.CreateAsync("https://example.com/first", CancellationToken.None);
        var second = await service.CreateAsync("https://example.com/second", CancellationToken.None);

        Assert.True(first.Created);
        Assert.True(second.Created);
        Assert.Equal("Abc1234", first.Mapping.ShortCode);
        Assert.Equal("XyZ9876", second.Mapping.ShortCode);
        Assert.Equal(2, await db.UrlMappings.CountAsync());
    }

    [Fact]
    public async Task FindByCodeAsync_UnknownCodeReturnsNull()
    {
        await using var db = await CreateDbContextAsync();
        var service = new UrlShortenerService(db, new QueueShortCodeGenerator("Abc1234"),
            NullLogger<UrlShortenerService>.Instance);

        var mapping = await service.FindByCodeAsync("NotHere", CancellationToken.None);

        Assert.Null(mapping);
    }

    private static async Task<ShortenerDbContext> CreateDbContextAsync()
    {
        var options = new DbContextOptionsBuilder<ShortenerDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var db = new ShortenerDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private sealed class QueueShortCodeGenerator(params string[] codes) : IShortCodeGenerator
    {
        private readonly Queue<string> _codes = new(codes);

        public string Generate()
        {
            if (_codes.Count == 0)
                throw new InvalidOperationException("No short-code value remains.");
            return _codes.Dequeue();
        }
    }
}
