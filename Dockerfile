# =============================================================================
# Stage 1: Build & Publish (.NET 10 SDK)
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for optimal Docker layer caching
COPY src/CabinetLogs.Domain/CabinetLogs.Domain.csproj src/CabinetLogs.Domain/
COPY src/CabinetLogs.Application/CabinetLogs.Application.csproj src/CabinetLogs.Application/
COPY src/CabinetLogs.Infrastructure/CabinetLogs.Infrastructure.csproj src/CabinetLogs.Infrastructure/
COPY src/CabinetLogs.Api/CabinetLogs.Api.csproj src/CabinetLogs.Api/

RUN dotnet restore src/CabinetLogs.Api/CabinetLogs.Api.csproj

# Copy full source code and publish
COPY src/ src/
RUN dotnet publish src/CabinetLogs.Api/CabinetLogs.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# =============================================================================
# Stage 2: Runtime (ASP.NET Core 10)
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Render injects PORT at runtime (default 10000); 8080 is used as local Docker fallback.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "CabinetLogs.Api.dll"]
