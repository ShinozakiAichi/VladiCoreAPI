# VladiCore API

VladiCore is a monolithic ASP.NET Web API 2 service backed by MySQL 8 that powers catalog, recommendations, and PC builder experiences.

## Prerequisites

- .NET Framework 4.8.1 build tooling (Visual Studio 2022 or `msbuild` on Windows).
- MySQL 8 locally or through Docker.
- PowerShell or Bash shell for scripts.

## Quick start

1. **Start MySQL via Docker**

   ```bash
   docker compose -f docker/docker-compose.yml up -d
   ```

2. **Create databases**

   ```sql
   CREATE DATABASE IF NOT EXISTS vladicore CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
   CREATE DATABASE IF NOT EXISTS vladicore_test CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
   ```

3. **Apply migrations and seeds**

   ```bash
   mysql -h 127.0.0.1 -P 3306 -u vladicore -pdevpass vladicore < db/migrations/mysql/001_init.sql
   mysql -h 127.0.0.1 -P 3306 -u vladicore -pdevpass vladicore < db/seed/001_seed_catalog.sql
   mysql -h 127.0.0.1 -P 3306 -u vladicore -pdevpass vladicore < db/seed/002_seed_pc_parts.sql
   mysql -h 127.0.0.1 -P 3306 -u vladicore -pdevpass vladicore < db/seed/003_seed_orders_views.sql
   ```

   Repeat the same for `vladicore_test` to hydrate the integration database.

4. **Configure connection string**

   Update `src/VladiCore.Api/Web.config` if your MySQL credentials differ.

5. **Restore NuGet packages & build**

   ```bash
   msbuild VladiCore.sln /p:RestorePackages=true
   ```

6. **Run the API**

   Launch `src/VladiCore.Api` from Visual Studio (IIS Express or self-host) or run via `Microsoft.Owin.Host.HttpListener`.

7. **Open Swagger**

   Navigate to `http://localhost:9000/swagger` (adjust base URL) to explore the API.

## Testing

Integration tests use the `vladicore_test` schema and will reseed data via SQL scripts.

```bash
msbuild VladiCore.Tests/VladiCore.Tests.csproj /t:VSTest
```

## Project layout

- `src/VladiCore.Api` – Web API surface, controllers, Swagger, Serilog, JWT.
- `src/VladiCore.Domain` – Entities, DTOs, value objects.
- `src/VladiCore.Data` – EF6 context, repositories, MySQL connection factory.
- `src/VladiCore.Recommendations` – Dapper-based aggregations for price history and co-purchases.
- `src/VladiCore.PcBuilder` – Compatibility rules and greedy auto-builder.
- `src/VladiCore.Tests` – NUnit + FluentAssertions unit/integration tests.
- `db` – SQL migrations and seed scripts.
- `docker` – Docker Compose and initialization scripts.
- `docs/api.http` – Ready-to-use HTTP request examples.

## Observability & logging

Serilog writes rolling log files to `App_Data/logs/log-*.txt` and the console. Each request receives a correlation id (`X-Correlation-Id`).

## Security

Admin endpoints require JWT bearer tokens. Configure issuer, audience, and signing key in `Web.config`. Ensure the signing key is rotated outside of source control for production.

