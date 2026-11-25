
# =============================================================================
# Stage 1: Build & Publish (.NET 10 SDK)
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/CabinetLogs.Domain/CabinetLogs.Domain.csproj src/CabinetLogs.Domain/
COPY src/CabinetLogs.Application/CabinetLogs.Application.csproj src/CabinetLogs.Application/
COPY src/CabinetLogs.Infrastructure/CabinetLogs.Infrastructure.csproj src/CabinetLogs.Infrastructure/
COPY src/CabinetLogs.Api/CabinetLogs.Api.csproj src/CabinetLogs.Api/

RUN dotnet restore src/CabinetLogs.Api/CabinetLogs.Api.csproj

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

# Install Kerberos dependency required by Npgsql
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "CabinetLogs.Api.dll"]
