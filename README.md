# VladiCore Shop API

VladiCore Shop is an ASP.NET Core 8 monolith that provides a full-featured commerce API: authentication with JWT/refresh tokens,
product catalog with rich filtering, review workflows, presigned uploads to S3/MinIO, price history, recommendations, and admin
operations. The service uses MySQL 8 as the primary data store and integrates with AWS S3-compatible storage for media assets.

## Features

- **Authentication & profiles**: user registration/login/refresh with 8+ character passwords (long passphrases supported),
  role-based policies (`User`, `Admin`), profile updates, and admin-controlled blocking.
- **Product catalog**: paged filtering by category/price/rating, full-text search, rating summaries, price history materialization,
  recommendations, and admin CRUD with automatic price history tracking.
- **Reviews**: authenticated review submission (configurable), owner edits within a configurable time window, soft deletion,
  admin moderation queue with approve/reject reasons, presigned photo confirmation, helpful voting, and automatic rating
  recomputation.
- **Storage**: presigned PUT URLs for product/review photos with prefix enforcement and CDN-aware public URLs.
- **Observability & resilience**: Serilog request logging with correlation IDs, RFC 7807 error responses, and rate limiting for
  review submissions.

## Quick start

### 1. Clone & configure

```bash
git clone <repo-url>
cd VladiCoreAPI
cp .env.example .env
```

Populate `.env` with:

- `ConnectionStrings__Default` – MySQL connection string.
- `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey`, `Jwt__AccessTokenTtlSeconds`, `Jwt__RefreshTokenTtlSeconds`.
- `S3__Endpoint`, `S3__Bucket`, `S3__AccessKey`, `S3__SecretKey`, `S3__UseSsl`, `S3__CdnBaseUrl`.
- `Reviews__RequireAuthentication` – set `true` to force authentication for review submissions & presign.
- `Reviews__UserEditWindowHours` – number of hours a non-admin author can edit their review (default `24`).

### 2. Launch infrastructure

Use Docker to run MySQL and MinIO:

```bash
docker compose -f docker/docker-compose.yml up -d
```

The compose file provisions MySQL with the schema scripts from `db/migrations/mysql` and MinIO for asset storage. Alternatively,
run the scripts manually against an existing MySQL instance.

### 3. Run the API

```bash
dotnet restore
dotnet run --project src/VladiCore.Api
```

Swagger UI is available at `http://localhost:5200/swagger`.

## Database provisioning

The `SchemaBootstrapper` inspects every SQL file in `db/migrations/mysql` before executing migrations. It builds a desired schema
plan, queries MySQL `information_schema` (`tables`, `columns`, `key_column_usage`, `referential_constraints`), and applies
idempotent fixes (`CREATE TABLE`, `ALTER TABLE ADD/MODIFY`, `ALTER TABLE ADD CONSTRAINT`) inside a single transaction before any
SQL script executes. Duplicate artifacts are reported as `MIGRATION_SKIP`, automatic fixes (type alignment, FK recreation) as
`MIGRATION_FIX`, and successful runs emit `MIGRATION_OK`. Sample log entries:

```
[INF] TABLE 'ProductReviews' EXISTS — SKIPPED
[INF] COLUMN 'Status' EXISTS — TYPE OK
[WRN] COLUMN 'ReviewId' TYPE mismatch — FIXING
[INF] FOREIGN KEY 'FK_ProductReviewVotes_Reviews' recreated with compatible type
```

Set `Database__AutoProvision__StrictMode=true` to fail fast on schema mismatches instead of attempting automatic fixes.

## Testing

```bash
dotnet test src/VladiCore.Tests
```

Tests rely on the in-memory provider and cover representative controller flows (catalog listing, review creation, and
infrastructure helpers). For full integration tests use the provided Docker Compose stack with Testcontainers.

## Presigned upload flow

1. `POST /storage/presign` with `type=products|reviews`, `contentType`, `size`, and `entityId`.
2. Upload the object to S3/MinIO via the returned URL and headers.
3. Confirm the upload via `POST /products/{id}/photos` or `POST /reviews/{id}/photos`.

## Seed data

Enable `Database__AutoProvision__ApplySeeds=true` to load sample hardware catalog data from `db/seed/002_seed_pc_parts.sql`.

## Environment variables

See `docs/README.md` for detailed environment, Docker, and provisioning documentation.

## Postman

Import `docs/api.http` (or export to Postman via VS Code REST Client) for sample requests covering the entire API contract.
