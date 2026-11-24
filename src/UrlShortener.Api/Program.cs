using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Api.Application;
using UrlShortener.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<ShortenerOptions>(builder.Configuration.GetSection(ShortenerOptions.SectionName));
var connectionString = builder.Configuration.GetConnectionString("Shortener") ?? "Data Source=data/shortener.db";
var sqliteConnection = new SqliteConnectionStringBuilder(connectionString);
if (sqliteConnection.DataSource != ":memory:")
{
    var databasePath = Path.GetFullPath(sqliteConnection.DataSource);
    Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
}

builder.Services.AddDbContext<ShortenerDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<IUrlShortenerService, UrlShortenerService>();
builder.Services.AddSingleton<IUrlNormalizer, UrlNormalizer>();
builder.Services.AddSingleton<IShortCodeGenerator, RandomBase62ShortCodeGenerator>();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<ShortenerDbContext>().Database.EnsureCreatedAsync();
}

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var isBadRequest = exception is BadHttpRequestException;
    context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("GlobalExceptionHandler")
        .Log(isBadRequest ? LogLevel.Information : LogLevel.Error, exception,
            "Request error for {Path}", context.Request.Path);
    context.Response.StatusCode = isBadRequest
        ? StatusCodes.Status400BadRequest
        : StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/problem+json";
    await context.Response.WriteAsJsonAsync(new ProblemDetails
    {
        Type = "about:blank",
        Title = isBadRequest ? "Bad request" : "An unexpected error occurred.",
        Status = context.Response.StatusCode,
        Detail = isBadRequest ? "The request body is invalid JSON or has an invalid shape." : null,
        Instance = context.Request.Path
    }, options: (JsonSerializerOptions?)null, contentType: "application/problem+json");
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapPost("/api/v1/urls", async (
    CreateUrlRequest? request,
    IUrlNormalizer normalizer,
    IUrlShortenerService service,
    Microsoft.Extensions.Options.IOptions<ShortenerOptions> options,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    if (!normalizer.TryNormalize(request?.LongUrl, out var normalizedUrl, out var error))
    {
        return Results.BadRequest(new ProblemDetails
        {
            Type = "about:blank",
            Title = "Invalid URL",
            Status = StatusCodes.Status400BadRequest,
            Detail = error,
            Instance = context.Request.Path
        });
    }

    var result = await service.CreateAsync(normalizedUrl, cancellationToken);
    var shortUrl = $"{options.Value.BaseUrl.TrimEnd('/')}/{result.Mapping.ShortCode}";
    var response = new CreateUrlResponse(result.Mapping.ShortCode, shortUrl,
        result.Mapping.LongUrl, result.Mapping.CreatedAtUtc);
    return result.Created ? Results.Created(shortUrl, response) : Results.Ok(response);
})
.WithName("CreateShortUrl")
.WithSummary("Create or retrieve a shortened URL")
.Produces<CreateUrlResponse>(StatusCodes.Status201Created)
.Produces<CreateUrlResponse>(StatusCodes.Status200OK)
.Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

app.MapGet("/{shortCode}", async (
    string shortCode,
    IUrlShortenerService service,
    CancellationToken cancellationToken) =>
{
    if (!ShortCodeRules.IsValid(shortCode)) return Results.NotFound();
    var mapping = await service.FindByCodeAsync(shortCode, cancellationToken);
    return mapping is null ? Results.NotFound() : Results.Redirect(mapping.LongUrl);
})
.WithName("ResolveShortUrl")
.WithSummary("Redirect to the original URL");

app.Run();

public partial class Program;
public sealed record CreateUrlRequest(string? LongUrl);
public sealed record CreateUrlResponse(string ShortCode, string ShortUrl, string LongUrl, DateTimeOffset CreatedAt);
