# VladiCore API

VladiCore is a monolithic ASP.NET Core 8.0 service backed by MySQL 8.0 that powers catalog, recommendations, analytics, and the PC builder experience.

## Prerequisites

- .NET 8 SDK (`dotnet --info` should report version 8.x).
- MySQL 8 locally or via Docker.
- PowerShell or Bash for scripts.

## Quick start

1. **Start MySQL via Docker**

   ```bash
   docker compose -f docker/docker-compose.yml up -d
   ```

2. **Create databases (if not using the bootstrap volume scripts)**

   ```sql
   CREATE DATABASE IF NOT EXISTS vladicore CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
   CREATE DATABASE IF NOT EXISTS vladicore_test CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
   ```

3. **Apply schema & seed data**

   ```bash
   mysql -h 127.0.0.1 -P 3306 -u vladicore -pdevpass vladicore < db/migrations/mysql/001_init.sql
   mysql -h 127.0.0.1 -P 3306 -u vladicore -pdevpass vladicore < db/seed/001_seed_catalog.sql
   mysql -h 127.0.0.1 -P 3306 -u vladicore -pdevpass vladicore < db/seed/002_seed_pc_parts.sql
   mysql -h 127.0.0.1 -P 3306 -u vladicore -pdevpass vladicore < db/seed/003_seed_orders_views.sql
   ```

   Repeat the same for `vladicore_test` when you need to hydrate the integration database.

4. **Restore NuGet packages & build**

   ```bash
   dotnet restore
   dotnet build
   ```

5. **Run EF Core migrations (optional)**

   The project ships with SQL scripts, but you can manage schema via EF Core:

   ```bash
   dotnet ef database update -p src/VladiCore.Data -s src/VladiCore.Api
   ```

6. **Run the API**

   ```bash
   dotnet run --project src/VladiCore.Api
   ```

7. **Open Swagger**

   Navigate to `http://localhost:5200/swagger` (adjust the port if you changed `launchSettings.json`).

## Testing

Integration tests target the `vladicore_test` schema and reseed the database from the SQL scripts before the first test run.

```bash
dotnet test
```

## Project layout

- `src/VladiCore.Api` – ASP.NET Core 8 API, controllers, Serilog, Swagger, JWT.
- `src/VladiCore.Domain` – Entities, DTOs, enums, value objects.
- `src/VladiCore.Data` – EF Core 8 `AppDbContext`, repositories, MySQL connection factory.
- `src/VladiCore.Recommendations` – Dapper-based aggregations for price history and co-purchases.
- `src/VladiCore.PcBuilder` – Compatibility rules and greedy auto-builder.
- `src/VladiCore.Tests` – NUnit + FluentAssertions integration tests.
- `db` – SQL migrations and seed scripts.
- `docker` – Docker Compose and initialization scripts.
- `docs/api.http` – Ready-to-use HTTP request examples.

## Observability & logging

Serilog writes rolling log files to `logs/api-*.log` and the console. Each request receives a correlation id via the `X-Correlation-Id` header.

## Security

Admin endpoints require JWT bearer tokens. Configure issuer, audience, and signing key in `appsettings.json`. Rotate secrets outside of source control for production.
