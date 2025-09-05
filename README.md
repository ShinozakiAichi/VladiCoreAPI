# VladiCoreAPI

Backend API for VladiCore built with .NET 8.

## Setup
Install EF Core CLI:
```bash
dotnet tool install --global dotnet-ef
```

Apply migrations:
```bash
dotnet ef migrations add InitialCreate -p src/VladiCore.Data -s src/VladiCore.Api
dotnet ef database update -p src/VladiCore.Data -s src/VladiCore.Api
```

The PostgreSQL connection string must include
`Include Error Detail=true; Search Path=public,core,catalog,sales,inventory`
so EF Core can access all schemas.

Example env variable:

```
ConnectionStrings__Postgres=Host=postgres;Port=5432;Database=shinozaki_db;Username=aichishinozaki;Password=aichishinozaki651;Include Error Detail=true;Search Path=public,core,catalog,sales,inventory
```

To allow requests from any origin:

```
Cors__AllowedOrigins__0=*
```

To limit CORS, list allowed origins explicitly:

```
Cors__AllowedOrigins__0=http://localhost:3000
Cors__AllowedOrigins__1=https://example.com
```



## Build
```bash
dotnet build
```

## Run
```bash
dotnet run --project src/VladiCore.Api
```

## Docker
```bash
docker build -t vladicore-api .
docker compose up -d
```
The compose file expects an existing Docker network `monitoring_default` with a running
PostgreSQL container named `postgres`. Credentials are predefined in the compose file.
To create the `citext` extension once if needed:

```bash
docker exec -it postgres psql -U postgres -d shinozaki_db -c "CREATE EXTENSION IF NOT EXISTS citext;"
```

Verify the API is up:

```bash
curl http://localhost:9000/health
curl http://localhost:9000/ping
```

## Test
```bash
dotnet test
```

## Sample requests
```bash
curl -X POST http://localhost:9000/auth/register -H "Content-Type: application/json" -d '{"email":"a@b.com","password":"Pass123!","username":"A"}'
curl -X POST http://localhost:9000/auth/login -H "Content-Type: application/json" -d '{"email":"a@b.com","password":"Pass123!"}'
curl http://localhost:9000/health
curl http://localhost:9000/ping
curl http://localhost:9000/api/categories
curl http://localhost:9000/api/products
```
