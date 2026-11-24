using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.Sqlite;

namespace UrlShortener.Tests;

public sealed class UrlShortenerApiTests : IClassFixture<ShortenerFactory>
{
    private readonly HttpClient _client;

    public UrlShortenerApiTests(ShortenerFactory factory) => _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Create_Returns201Then200WithSameShortUrl()
    {
        const string longUrl = "https://www.example.com/products/shoes?size=42";

        var first = await _client.PostAsJsonAsync("/api/v1/urls", new { longUrl });
        var firstBody = await first.Content.ReadFromJsonAsync<CreateResponse>();
        var second = await _client.PostAsJsonAsync("/api/v1/urls", new { longUrl });
        var secondBody = await second.Content.ReadFromJsonAsync<CreateResponse>();

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(firstBody!.ShortCode, secondBody!.ShortCode);
        Assert.Equal(firstBody.ShortUrl, secondBody.ShortUrl);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(firstBody.ShortUrl, first.Headers.Location?.ToString());
    }

    [Fact]
    public async Task ConcurrentCreates_ForSameNormalizedUrl_ReturnSameCode()
    {
        var requests = Enumerable.Range(0, 8).Select(_ =>
            _client.PostAsJsonAsync("/api/v1/urls", new { longUrl = "  HTTPS://Example.com/  " }));

        var responses = await Task.WhenAll(requests);
        var bodies = await Task.WhenAll(responses.Select(response =>
            response.Content.ReadFromJsonAsync<CreateResponse>()));

        Assert.All(responses, response => Assert.True(
            response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            $"Unexpected status: {response.StatusCode}"));
        Assert.Single(bodies.Select(body => body!.ShortCode).Distinct());
    }

    [Fact]
    public async Task Resolve_Returns302WithOriginalLocation()
    {
        const string longUrl = "https://example.com/landing";
        var created = await _client.PostAsJsonAsync("/api/v1/urls", new { longUrl });
        var body = await created.Content.ReadFromJsonAsync<CreateResponse>();

        var response = await _client.GetAsync($"/{body!.ShortCode}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(longUrl, response.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("file:///tmp/file")]
    public async Task Create_InvalidInputReturnsProblemDetails(string? longUrl)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/urls", new { longUrl });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("about:blank", body.GetProperty("type").GetString());
        Assert.True(body.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task Create_MalformedJsonReturnsProblemDetails()
    {
        using var content = new StringContent("{\"longUrl\":", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/v1/urls", content);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("about:blank", body.GetProperty("type").GetString());
        Assert.True(body.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task Resolve_UnknownCodeReturns404()
    {
        var response = await _client.GetAsync("/unknown1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record CreateResponse(string ShortCode, string ShortUrl, string LongUrl, DateTimeOffset CreatedAt);
}

public sealed class ShortenerFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"shortener-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Shortener", $"Data Source={_databasePath};Default Timeout=10");
        builder.UseSetting("Shortener:BaseUrl", "https://short.test");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Shortener"] = $"Data Source={_databasePath};Default Timeout=10",
                ["Shortener:BaseUrl"] = "https://short.test"
            }));
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;

        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();

        foreach (var path in new[]
                 {
                     _databasePath,
                     $"{_databasePath}-shm",
                     $"{_databasePath}-wal"
                 })
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}