# URL Shortener Microservice

A small ASP.NET Core URL shortener with SQLite persistence, deterministic normalization, random Base62 codes, and API/integration tests. The project targets .NET 10 (supported under the assignment's .NET 8 or later requirement).

## Build and run

Requirements: .NET 10 SDK. SQLite is embedded; no separate database service is needed.

```powershell
dotnet restore UrlShortener.slnx
dotnet build UrlShortener.slnx
dotnet test UrlShortener.slnx
dotnet run --project src/UrlShortener.Api
```

The API listens on the URL printed at startup. In Development, Swagger UI is available at `/swagger`. The local SQLite database is created at `data/shortener.db` relative to the process working directory when running the project directly. Override settings with `ConnectionStrings__Shortener` and `Shortener__BaseUrl` environment variables (or the equivalent appsettings entries). For a real deployment, set `Shortener:BaseUrl` to the public origin that will serve redirects.

Start the container and persistent volume with Docker Compose:

```powershell
docker compose up --build
```

This publishes the service at `http://localhost:8080`; Compose persists its SQLite file in the `shortener-data` volume. The compose file sets the base URL to that local origin for convenience.

## API

Create a short URL:

```powershell
curl.exe -i -X POST http://localhost:8080/api/v1/urls `
  -H "Content-Type: application/json" `
  -d '{"longUrl":"https://www.example.com/products/category/shoes?color=red&size=42"}'
```

A first submission returns `201 Created` with a `Location` header. Repeating it returns `200 OK` with the same code and URL. The response is shaped as:

```json
{
  "shortCode": "aZ3kQ9x",
  "shortUrl": "http://localhost:8080/aZ3kQ9x",
  "longUrl": "https://www.example.com/products/category/shoes?color=red&size=42",
  "createdAt": "2026-10-21T09:15:00+00:00"
}
```

Resolve (the `-i` flag shows the 302 `Location`; add `-L` to follow it):

```powershell
curl.exe -i http://localhost:8080/aZ3kQ9x
```

Invalid input returns `400` with an RFC 9457 `ProblemDetails` body. Unknown codes return `404`. Unexpected exceptions return `500` with a generic ProblemDetails body; details are logged server-side. Health check:

```powershell
curl.exe -i http://localhost:8080/health
```

## Design notes

- **API style:** Minimal APIs keep this small service's HTTP surface concise. Application behavior is behind injected interfaces, and persistence is isolated in the infrastructure layer. This makes endpoint behavior easy to test without coupling business logic to HTTP.
- **Codes:** A cryptographic random generator independently selects each of seven characters from the Base62 alphabet (`a-z`, `A-Z`, `0-9`). The space is $62^7$ (about 3.5 trillion) codes. Random codes are compact and do not expose insertion order, but collisions become more likely as the space fills. A unique database index on code detects collisions; creation retries with a new code up to eight times.
- **Idempotency and concurrency:** Input is normalized before storage and lookup. A unique database index on `NormalizedLongUrl` is the final arbiter, so parallel requests cannot create duplicate mappings. If an insert loses that race, the service detaches its failed entity and fetches the winning mapping, returning it as an existing result. The same unique-index/collision-retry path also handles generated-code collisions.
- **Normalization:** Leading/trailing whitespace is trimmed; scheme and host are lowercased; a terminal slash is removed for a URL whose path is `/`, preserving any query or fragment. `Uri` canonicalization is applied; path/query case and non-root trailing slashes are retained. The canonical normalized string is used as both the stored destination and comparison key, so root-path variants resolve to the normalized root URL.
- **Validation:** Only absolute HTTP and HTTPS URLs with a host are accepted; the submitted string must be no more than 2,048 characters.
- **Persistence:** EF Core SQLite stores both unique constraints and mappings in a file-backed database, so records survive restarts. The container uses a named volume.
- **Tests:** xUnit tests cover code generation/rules and normalization, while `WebApplicationFactory` tests cover creation, repeats, concurrent submissions, redirect, invalid input, and unknown codes.

## Assumptions and future improvements

The short domain is deployment configuration rather than a DNS setup performed by the app. SQLite is appropriate for a small single-instance service; a high-write multi-instance deployment should use PostgreSQL or SQL Server with the same unique constraints, migrations, and transaction logic. Before production, add schema migrations instead of `EnsureCreated`, rate limiting/abuse controls, metrics and tracing, retention/administrative workflows, and a deployment-specific health check that verifies database readiness. URLs are stored in normalized form, and code generation uses seven characters throughout the allowed 6–10 character range.
