# syntax=docker/dockerfile:1.6
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["VladiCore.sln", "./"]
COPY ["src/VladiCore.Api/VladiCore.Api.csproj", "src/VladiCore.Api/"]
COPY ["src/VladiCore.Data/VladiCore.Data.csproj", "src/VladiCore.Data/"]
COPY ["src/VladiCore.Domain/VladiCore.Domain.csproj", "src/VladiCore.Domain/"]
COPY ["src/VladiCore.PcBuilder/VladiCore.PcBuilder.csproj", "src/VladiCore.PcBuilder/"]
COPY ["src/VladiCore.Recommendations/VladiCore.Recommendations.csproj", "src/VladiCore.Recommendations/"]
# Restore only the API project to avoid pulling in test projects that are not part of the build context
RUN dotnet restore "src/VladiCore.Api/VladiCore.Api.csproj"
COPY . .

# Copy SQL scripts into a temporary location in the build stage so they can be
# moved to the final image after publish.
RUN mkdir -p /tmp/db/migrations/mysql /tmp/db/seed && \
    if [ -d "db/migrations/mysql" ]; then cp -a db/migrations/mysql/. /tmp/db/migrations/mysql/; fi && \
    if [ -d "db/seed" ]; then cp -a db/seed/. /tmp/db/seed/; fi
WORKDIR "/src/src/VladiCore.Api"
RUN dotnet publish "VladiCore.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish ./

# Move SQL scripts from the build stage into the final image so the
# SchemaBootstrapper can discover and execute them at runtime.
COPY --from=build /tmp/db /app/db
ENTRYPOINT ["dotnet", "VladiCore.Api.dll"]
