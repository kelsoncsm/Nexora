FROM mcr.microsoft.com/dotnet/sdk:10.0.400 AS build
WORKDIR /source

COPY Directory.Build.props Directory.Packages.props Nexora.slnx ./
COPY src/Nexora.Domain/Nexora.Domain.csproj src/Nexora.Domain/
COPY src/Nexora.Application/Nexora.Application.csproj src/Nexora.Application/
COPY src/Nexora.Infrastructure/Nexora.Infrastructure.csproj src/Nexora.Infrastructure/
COPY src/Nexora.Api/Nexora.Api.csproj src/Nexora.Api/
COPY tests/Nexora.UnitTests/Nexora.UnitTests.csproj tests/Nexora.UnitTests/
COPY tests/Nexora.IntegrationTests/Nexora.IntegrationTests.csproj tests/Nexora.IntegrationTests/
RUN dotnet restore Nexora.slnx

COPY . .
RUN dotnet tool restore
RUN dotnet test Nexora.slnx \
    --configuration Release \
    --no-restore \
    --maxcpucount:1 \
    --nodeReuse:false \
    -p:UseSharedCompilation=false
RUN dotnet publish src/Nexora.Api/Nexora.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish
RUN ConnectionStrings__NexoraDatabase="Host=localhost;Database=nexora;Username=nexora;Password=build-only" dotnet ef migrations bundle \
    --project src/Nexora.Infrastructure/Nexora.Infrastructure.csproj \
    --startup-project src/Nexora.Infrastructure/Nexora.Infrastructure.csproj \
    --context NexoraDbContext \
    --configuration Release \
    --no-build \
    --output /app/migrate

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
COPY --from=build /app/migrate /app/migrate
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 CMD ["curl", "--fail", "--silent", "http://localhost:8080/health/live"]
ENTRYPOINT ["dotnet", "Nexora.Api.dll"]
