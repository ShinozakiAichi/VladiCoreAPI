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
WORKDIR "/src/src/VladiCore.Api"
RUN dotnet publish "VladiCore.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "VladiCore.Api.dll"]
