# Cabinet Logs Backend

REST API for managing cabinet and equipment maintenance logs, built with **ASP.NET Core (.NET 10)**, **PostgreSQL (Supabase)**, **Supabase Storage**, **ClosedXML**, and **Scalar**.

## Overview

This backend powers desktop and mobile clients for tracking equipment issues and maintenance history.

### Key Features

- **Automatic Cabinet Categorization:** Cabinets are created and deduplicated automatically when logs are submitted.
- **Maintenance Logs CRUD:** Create, view, search, filter (by cabinet, issue text, and date range), sort, paginate, edit, and delete logs.
- **Client Device Tracking:** Registers client installations and records which device created each log.
- **Private Multi-Photo Storage:** Supports attaching multiple images per log (JPEG, PNG, WebP) stored in a private bucket and accessed via short-lived signed URLs, with automatic orphan cleanup.
- **Excel Reports:** Exports cabinet logs to `.xlsx` workbooks for a selected cabinet and optional date range.
- **Interactive API Reference:** OpenAPI documentation served with **Scalar**.

## Tech Stack

- **Runtime:** .NET 10 / ASP.NET Core Web API
- **Database:** PostgreSQL via Entity Framework Core (`Npgsql`)
- **Object Storage:** Supabase Storage (private bucket + signed URLs)
- **Excel Generation:** ClosedXML
- **API Docs:** Microsoft.AspNetCore.OpenApi + Scalar.AspNetCore
- **Containerization:** Docker (multi-stage build)

## Project Structure

```text
├── Dockerfile
├── CabinetLogs.slnx
├── src/
│   ├── CabinetLogs.Domain/         # Core entities, enums, domain logic
│   ├── CabinetLogs.Application/    # DTOs, interfaces, validation, services
│   ├── CabinetLogs.Infrastructure/ # EF Core DbContext, migrations, storage, Excel reports
│   └── CabinetLogs.Api/            # Controllers, middleware, OpenAPI/Scalar setup
└── tests/
    └── CabinetLogs.Tests/          # Unit and integration tests
```

## API Overview

Base route: `/api/v1`

- `GET /api/v1/health` — API and database health check
- `/api/v1/client-devices` — Client device registration and management
- `/api/v1/cabinets` — Cabinet listing, search, logs, and deletion
- `/api/v1/logs` — Maintenance logs CRUD, filtering, pagination, and signed photo URLs
- `GET /api/v1/reports/cabinets/{cabinetId}/excel` — Download `.xlsx` report for a cabinet

## Running Locally

1. Configure your database and storage settings in `src/CabinetLogs.Api/appsettings.Local.json` (see `src/CabinetLogs.Api/appsettings.example.json`) or via environment variables.
2. Apply migrations and run the API:

```powershell
dotnet ef database update --project src/CabinetLogs.Infrastructure --startup-project src/CabinetLogs.Api
dotnet run --project src/CabinetLogs.Api
```

## Running Tests

```powershell
dotnet test CabinetLogs.slnx
```
