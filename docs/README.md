# VladiCore API

VladiCore is a monolithic ASP.NET Core 8.0 service backed by MySQL 8.0 that powers catalog, recommendations, analytics, and the PC builder experience.

## API surface at a glance

| Area | Endpoint highlights |
| --- | --- |
| Auth | `POST /auth/register`, `POST /auth/login`, `POST /auth/refresh` |
| Profiles | `GET /me`, `PUT /me`, `POST /users/{id}/block`, `DELETE /users/{id}/block` |
| Catalog | `GET /products`, `GET /products/{id}`, `GET /products/{id}/price-history`, `GET /products/{id}/recommendations` |
| Admin catalog | `POST /products`, `PUT /products/{id}`, `DELETE /products/{id}`, `POST /products/{id}/photos`, `DELETE /products/{id}/photos/{photoId}` |
| Reviews | `GET /products/{id}/reviews`, `POST /products/{id}/reviews`, `PATCH /products/{productId}/reviews/{reviewId}`, `DELETE /products/{productId}/reviews/{reviewId}`, `POST /products/{productId}/reviews/{reviewId}/photos`, `POST /products/{productId}/reviews/{reviewId}/vote` |
| Review moderation | `GET /reviews/moderation`, `POST /reviews/{reviewId}/approve`, `POST /reviews/{reviewId}/reject` |
| Storage | `POST /storage/presign` |

See `docs/api.http` for ready-to-run request samples.

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
| `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey` | JWT bearer token configuration. `Jwt__SigningKey` must resolve to at least 16 bytes (128 bits); generate one with `openssl rand -base64 32`. |
| `Jwt__AccessTokenTtlSeconds`, `Jwt__RefreshTokenTtlSeconds` | Lifetimes (in seconds) for issued access and refresh tokens. |
| `S3__Endpoint`, `S3__Bucket`, `S3__AccessKey`, `S3__SecretKey`, `S3__UseSsl`, `S3__CdnBaseUrl` | S3/MinIO-compatible object storage used for product assets and review photos. |
| `Reviews__RequireAuthentication` | Set to `true` to require authenticated users for review submission and presign operations. |
| `Reviews__UserEditWindowHours` | Hours an author may edit their review before it is locked. |
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

3. **Let the API auto-provision the schema**

   The API applies any missing migrations from `db/migrations/mysql` every time it starts. By default it also creates the database if it is missing and skips seed scripts unless `Database__AutoProvision__ApplySeeds=true` (see below).

   > 💡 You can still run the SQL scripts manually when you want to hydrate a database before the API starts or when debugging migrations locally.

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

## Database auto-provisioning

The API automatically provisions the schema on startup using the SQL scripts in `db/migrations/mysql`.

- Feature flags live under the `Database:AutoProvision` section (see `appsettings.json`) and can be overridden via environment variables such as `Database__AutoProvision__Enabled`.
- Default behaviour: create the database if it is missing, apply pending migrations, and skip seed scripts. Set `Database__AutoProvision__ApplySeeds=true` for local development when you need sample data.
- Scripts must follow the `NNN_description.sql` naming convention. The provisioner records applied files in the `schema_migrations` table and runs each script inside its own transaction.
- Log events: `DB_INIT_START`, `MIGRATION_APPLY_START`, `MIGRATION_APPLY_OK`, `MIGRATION_APPLY_FAIL`, `SEED_APPLY_START`, `SEED_APPLY_OK`, `SEED_APPLY_FAIL`, `DB_INIT_DONE`.
- Health probe: `GET /health/db` checks connectivity, confirms sentinel tables (e.g. `Products`, `Orders`), and returns the count of applied migrations. It returns `503` when the database is unavailable or missing tables.

Manual execution of the SQL files remains supported as a fallback or for debugging purposes.

## Testing

Integration tests target the `vladicore_test` schema. The test fixture reuses the same auto-provisioner to create the schema and populate seeds when a MySQL instance is available; tests are skipped otherwise.

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
