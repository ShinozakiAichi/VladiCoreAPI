# VladiCore API

VladiCore is a monolithic ASP.NET Core 8.0 service backed by MySQL 8.0 that powers catalog, recommendations, analytics, and the PC builder experience.

## Prerequisites

- .NET 8 SDK (`dotnet --info` should report version 8.x).
- Docker 24+ (for containerised workflows).
- MySQL 8 locally or via Docker.
- PowerShell or Bash for scripts.

## Configuration

All runtime configuration is sourced from environment variables. Copy the template and adjust values for your environment:

```bash
cp .env.example .env
```

Key variables:

| Variable | Purpose |
| --- | --- |
| `ConnectionStrings__Default` | MySQL connection string used by the API. |
| `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey` | JWT bearer token configuration. Always replace the signing key. |
| `S3__Endpoint`, `S3__Bucket`, `S3__AccessKey`, `S3__SecretKey`, `S3__UseSsl`, `S3__CdnBaseUrl` | S3/MinIO-compatible object storage used for product assets and review photos. |
| `Reviews__RequireAuthentication` | Set to `true` to require authenticated users for review submission and presign operations. |
| `ASPNETCORE_URLS` | HTTP binding inside the container (defaults to port `8080`). |
| `API_HTTP_PORT` | Host port exposed by Docker Compose. |
| `DOCKER_NETWORK_NAME` | Shared Docker network that contains both the API and database containers. |
| `DOCKER_NETWORK_EXTERNAL` | Set to `true` when the shared network is created outside of Docker Compose. |

> ℹ️ Secrets such as the JWT signing key must be rotated regularly and should be stored in a secure secret manager for production deployments.

## Quick start

### Using Docker Compose (API attached to an external MySQL container)

1. **Prepare configuration**

   ```bash
   cp .env.example .env # customise before first run
   ```

   Update `ConnectionStrings__Default` to point to the hostname of the existing MySQL container. Both containers must share a user-defined Docker network (see the next step) and `DOCKER_NETWORK_NAME`/`DOCKER_NETWORK_EXTERNAL` should match how that network is managed.

2. **Ensure network connectivity to the database container**

   Create a shared network if you do not already have one and connect the database container to it:

   ```bash
   docker network create vladicore-backend # skip if the network already exists
   docker network connect vladicore-backend <your-mysql-container-name>
   ```

   If you start the MySQL container yourself, run it on the same network, for example:

   ```bash
   docker run -d \
     --name vladicore-mysql \
     --network vladicore-backend \
     -e MYSQL_ROOT_PASSWORD=rootpass \
     -e MYSQL_DATABASE=vladicore \
     -e MYSQL_USER=vladicore \
     -e MYSQL_PASSWORD=devpass \
     mysql:8.0 --default-authentication-plugin=mysql_native_password
   ```

3. **Start MinIO (optional but recommended for local image uploads)**

   ```bash
   docker run -d \
     --name vladicore-minio \
     -p 9000:9000 \
     -p 9001:9001 \
     -e MINIO_ROOT_USER=minioadmin \
     -e MINIO_ROOT_PASSWORD=minioadmin \
     quay.io/minio/minio server /data --console-address ":9001"
   ```

   Update the `S3__Endpoint`/`S3__Bucket` variables if you choose a different host or bucket name.

4. **Start the API container**

   ```bash
   docker compose -f docker/docker-compose.yml up --build -d
   ```

   The API will listen on `http://localhost:${API_HTTP_PORT:-8080}` and connect to the MySQL container through the shared network using the provided connection string.

### Local development (hosted runtime)

1. **Start MySQL via Docker (or connect to an existing container)**

   ```bash
   docker run -d \
     --name vladicore-mysql \
     -p 3306:3306 \
     -e MYSQL_ROOT_PASSWORD=rootpass \
     -e MYSQL_DATABASE=vladicore \
     -e MYSQL_USER=vladicore \
     -e MYSQL_PASSWORD=devpass \
     mysql:8.0 --default-authentication-plugin=mysql_native_password
   ```

   Alternatively, reuse a neighbour container and update the connection string accordingly.

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

   > 💡 The API now applies pending scripts from `db/migrations/mysql` on startup. Running them manually remains useful for local debugging or to hydrate the schema before the service is available.

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

   The default launch settings expose the API on `https://localhost:7200` and `http://localhost:5200`.

7. **Open Swagger**

   Navigate to `http://localhost:5200/swagger` (adjust the port if you changed `launchSettings.json`).

## Testing

Integration tests target the `vladicore_test` schema and reseed the database from the SQL scripts before the first test run.

```bash
dotnet test
```

## Project layout

- `src/VladiCore.Api` – ASP.NET Core 8 API, controllers, Serilog, Swagger, JWT.
- `src/VladiCore.Api/Controllers/ReviewsController` – product review CRUD with moderation workflow.
- `src/VladiCore.Api/Controllers/UploadsController` – presigned S3 uploads for review photos.
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

Public catalog, analytics, tracking, and PC builder endpoints now allow anonymous access. Catalogue mutations, review moderation, and product asset uploads require the `Admin` role, while presign operations honour the `Reviews__RequireAuthentication` flag and accept authenticated `User` or `Admin` tokens.
