using Microsoft.EntityFrameworkCore;
using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Infrastructure;

public sealed class ShortenerDbContext(DbContextOptions<ShortenerDbContext> options) : DbContext(options)
{
    public DbSet<UrlMapping> UrlMappings => Set<UrlMapping>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var mapping = modelBuilder.Entity<UrlMapping>();
        mapping.HasKey(item => item.Id);
        mapping.Property(item => item.ShortCode).HasMaxLength(10).IsRequired();
        mapping.Property(item => item.NormalizedLongUrl).HasMaxLength(2048).IsRequired();
        mapping.Property(item => item.LongUrl).HasMaxLength(2048).IsRequired();
        mapping.HasIndex(item => item.ShortCode).IsUnique();
        mapping.HasIndex(item => item.NormalizedLongUrl).IsUnique();
    }
}