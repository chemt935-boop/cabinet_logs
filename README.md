# Cabinet Logs Backend (.NET 10 + Supabase + Scalar)

REST API for storing cabinet and equipment maintenance/issue logs, built with **ASP.NET Core (.NET 10)**, **EF Core + Npgsql (Supabase PostgreSQL)**, **Supabase Storage** (private photo uploads + short-lived signed URLs), **ClosedXML** (Excel `.xlsx` reporting without photos), and **Scalar** for interactive OpenAPI documentation.

---

## 1. Where to Put Your Actual Data (Checklist)

All configuration files committed to this repository contain **placeholder values (`REPLACE_WITH_...`) only**.
Below is every value you need to replace, **where to get it**, and **where to put it**.

### Quick Summary of Values You Need

| Setting Key | Placeholder in Config | Where to Get It in Supabase | Required? |
|---|---|---|---|
| `ConnectionStrings:DefaultConnection` | `Host=REPLACE_WITH_SUPABASE_DB_HOST;...` | Supabase Dashboard → **Connect** (top bar) or **Project Settings → Database → Connection string** (`.NET` / `URI` / Session or Transaction Pooler) | **Yes** |
| `Supabase:Url` | `https://REPLACE_WITH_YOUR_PROJECT_REF.supabase.co` | Supabase Dashboard → **Project Settings → API → Project URL** | **Yes** (for photos) |
| `Supabase:ServiceRoleKey` | `REPLACE_WITH_SUPABASE_SERVICE_ROLE_KEY` | Supabase Dashboard → **Project Settings → API → `service_role` (secret)** *(Server-side ONLY — NEVER put in Flutter!)* | **Yes** (for photos) |
| `Supabase:AnonKey` | `REPLACE_WITH_SUPABASE_ANON_KEY` | Supabase Dashboard → **Project Settings → API → `anon` (public)** | Optional |
| `Supabase:StorageBucket` | `cabinet-log-photos` | Supabase Dashboard → **Storage → Buckets** (create a **private** bucket with this name, or change the name here to match yours) | **Yes** (for photos) |
| `Supabase:SignedUrlExpirySeconds` | `300` | Your preference (default `300` = 5 minutes) | Optional |
| `PhotoUpload:MaxFileSizeBytes` | `5242880` | Your preference (default `5242880` = 5 MB) | Optional |
| `PhotoUpload:AllowedContentTypes` | `["image/jpeg","image/png","image/webp"]` | Your preference | Optional |
| `ApiSecurity:RequireApiKey` | `false` | Set `true` before exposing to the public internet (v1 defaults to `false` no-login shared access) | Optional (recommended for public deploy) |
| `ApiSecurity:ApiKey` | `REPLACE_WITH_STRONG_RANDOM_API_KEY_IF_ENABLED` | Generate any long random string (only used when `RequireApiKey` is `true`) | Only if `RequireApiKey = true` |
| `Scalar:Enabled` | `true` (Dev) / `false` (Prod) | Controls whether `/scalar` and `/openapi/v1.json` are served | Optional |
| `Cors:AllowedOrigins` | `["*"]` | Replace `"*"` with your actual web/app origins before public production | Optional |

---

### Choose ONE of These 3 Ways to Supply Your Real Values

> [!IMPORTANT]
> Never commit real passwords or your Supabase `service_role` key to Git!

#### Option A — Local Override File (Easiest for Local Development, Ignored by Git)

1. Copy [`src/CabinetLogs.Api/appsettings.example.json`](src/CabinetLogs.Api/appsettings.example.json) to **`src/CabinetLogs.Api/appsettings.Local.json`** (this filename is already listed in `.gitignore` and automatically loaded by `Program.cs`).
2. Replace every `REPLACE_WITH_...` value inside `src/CabinetLogs.Api/appsettings.Local.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=aws-0-eu-central-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.YOUR_PROJECT_REF;Password=YOUR_ACTUAL_DB_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
  },
  "Supabase": {
    "Url": "https://YOUR_PROJECT_REF.supabase.co",
    "AnonKey": "YOUR_ACTUAL_ANON_KEY",
    "ServiceRoleKey": "YOUR_ACTUAL_SERVICE_ROLE_KEY",
    "StorageBucket": "cabinet-log-photos",
    "SignedUrlExpirySeconds": 300
  },
  "PhotoUpload": {
    "MaxFileSizeBytes": 5242880,
    "AllowedContentTypes": [
      "image/jpeg",
      "image/png",
      "image/webp"
    ]
  },
  "ApiSecurity": {
    "RequireApiKey": false,
    "ApiKeyHeaderName": "X-Api-Key",
    "ApiKey": "YOUR_OPTIONAL_SHARED_API_KEY"
  },
  "Scalar": {
    "Enabled": true
  },
  "Cors": {
    "AllowedOrigins": [
      "*"
    ]
  }
}
```

> **Note on Supabase PostgreSQL Connection Strings:**
> - **Direct connection (IPv6-capable networks):** `Host=db.<PROJECT_REF>.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=<DB_PASSWORD>;SSL Mode=Require;Trust Server Certificate=true`
> - **Session Pooler (IPv4-friendly — recommended on most Windows dev machines & cloud hosts):** `Host=aws-0-<REGION>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<PROJECT_REF>;Password=<DB_PASSWORD>;SSL Mode=Require;Trust Server Certificate=true`
> - **Transaction Pooler (Port `6543`):** If you use port `6543`, append `;No Reset On Close=true;Max Auto Prepare=0` so Npgsql prepared statements don't conflict with PgBouncer transaction pooling.

#### Option B — .NET User Secrets (Recommended for Local Development)

Run from the repository root (`e:\Cabinet_Logs`):

```powershell
dotnet user-secrets --project src/CabinetLogs.Api set "ConnectionStrings:DefaultConnection" "Host=YOUR_HOST;Port=5432;Database=postgres;Username=YOUR_USER;Password=YOUR_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
dotnet user-secrets --project src/CabinetLogs.Api set "Supabase:Url" "https://YOUR_PROJECT_REF.supabase.co"
dotnet user-secrets --project src/CabinetLogs.Api set "Supabase:AnonKey" "YOUR_ANON_KEY"
dotnet user-secrets --project src/CabinetLogs.Api set "Supabase:ServiceRoleKey" "YOUR_SERVICE_ROLE_KEY"
dotnet user-secrets --project src/CabinetLogs.Api set "Supabase:StorageBucket" "cabinet-log-photos"
```

#### Option C — Environment Variables (Recommended for Production / Docker / Cloud Hosting)

Use double underscores (`__`) for nested JSON keys:

```bash
ConnectionStrings__DefaultConnection="Host=...;Port=5432;Database=postgres;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true"
Supabase__Url="https://YOUR_PROJECT_REF.supabase.co"
Supabase__AnonKey="YOUR_ANON_KEY"
Supabase__ServiceRoleKey="YOUR_SERVICE_ROLE_KEY"
Supabase__StorageBucket="cabinet-log-photos"
Supabase__SignedUrlExpirySeconds="300"
PhotoUpload__MaxFileSizeBytes="5242880"
ApiSecurity__RequireApiKey="true"
ApiSecurity__ApiKeyHeaderName="X-Api-Key"
ApiSecurity__ApiKey="YOUR_STRONG_RANDOM_API_KEY"
Scalar__Enabled="false"
Cors__AllowedOrigins__0="https://your-allowed-origin.example.com"
```

---

## 2. Supabase Setup (Database & Private Photo Bucket)

### Step 1: Create the Database Tables

Choose **either** EF Core Migrations **or** the provided SQL script:

- **Way 1 — Apply EF Core Migration (Recommended):**
  Once your `ConnectionStrings:DefaultConnection` is set (via Option A, B, or C above), run:
  ```powershell
  dotnet ef database update --project src/CabinetLogs.Infrastructure --startup-project src/CabinetLogs.Api
  ```

- **Way 2 — Run SQL Script in Supabase SQL Editor:**
  Open Supabase Dashboard → **SQL Editor → New Query**, paste the contents of [`src/CabinetLogs.Infrastructure/Persistence/Sql/supabase_init.sql`](src/CabinetLogs.Infrastructure/Persistence/Sql/supabase_init.sql), and click **Run**.
  This creates `cabinets`, `client_devices`, `logs`, `pending_storage_cleanups`, all indexes, foreign keys, enables Row Level Security (with no public PostgREST policies), and creates the private `cabinet-log-photos` bucket.

### Step 2: Verify the Private Storage Bucket

In Supabase Dashboard → **Storage**:
1. Ensure a bucket named **`cabinet-log-photos`** (or whatever you set in `Supabase:StorageBucket`) exists.
2. Confirm **Public bucket** is **OFF** (private). Photos are only accessed via short-lived signed URLs returned by `GET /api/v1/logs/{id}/photo-url`.

### Step 3: Row Level Security (RLS) Reminder

If you applied the schema using `dotnet ef database update` instead of `supabase_init.sql`, also run these 4 lines in Supabase SQL Editor to lock down direct PostgREST access:

```sql
ALTER TABLE public.cabinets ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.client_devices ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.logs ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.pending_storage_cleanups ENABLE ROW LEVEL SECURITY;
```

Because no public `anon`/`authenticated` policies are created, the tables cannot be read or modified directly from Supabase's public API — only your .NET backend can access them.

---

## 3. Run the API & Open Scalar

Start the backend from the repository root:

```powershell
dotnet run --project src/CabinetLogs.Api
```

Then open **Scalar API Reference** in your browser:

- **Scalar UI:** `https://localhost:7018/scalar` or `http://localhost:5124/scalar` (visiting `/` in Development automatically redirects to `/scalar`)
- **OpenAPI JSON:** `http://localhost:5124/openapi/v1.json`
- **Health Check:** `http://localhost:5124/api/v1/health` (verifies PostgreSQL connectivity)

> In Scalar, both `POST /api/v1/logs` and `PUT /api/v1/logs/{id}` offer a **Request Body** dropdown allowing you to switch between `application/json` (no photo) and `multipart/form-data` (with file upload picker).

---

## 4. API Endpoints Reference (`/api/v1`)

### Health
| Method | Endpoint | Purpose |
|---|---|---|
| `GET` | `/api/v1/health` | Check API liveness & PostgreSQL connectivity |

### Client Devices
| Method | Endpoint | Purpose |
|---|---|---|
| `POST` | `/api/v1/client-devices` | Register a PC/mobile installation (`platform`: `Windows`, `MacOS`, `Android`, `IOS`, `Other`) |
| `GET` | `/api/v1/client-devices` | List/search registered devices (`?search=&installationKey=&page=1&pageSize=20`) |
| `GET` | `/api/v1/client-devices/{id}` | Get one registered device |
| `PUT` | `/api/v1/client-devices/{id}` | Update device name/platform (historical log snapshots remain unchanged) |
| `DELETE` | `/api/v1/client-devices/{id}` | Delete device (`logs.client_device_id` becomes `null`; logs & snapshots are preserved) |

### Cabinets (Auto-Created Categories)
| Method | Endpoint | Purpose |
|---|---|---|
| `GET` | `/api/v1/cabinets` | List/search cabinets with `logCount` and `lastOccurredAt` (`?search=LAF&page=1&pageSize=20`) |
| `GET` | `/api/v1/cabinets/{id}` | Get cabinet details and log count |
| `GET` | `/api/v1/cabinets/{id}/logs` | Get paginated/filtered logs for a single cabinet |
| `DELETE` | `/api/v1/cabinets/{id}` | Delete a cabinet (returns `409 Conflict` if it still has logs unless `?force=true` is passed) |

### Logs & Photos
| Method | Endpoint | Purpose |
|---|---|---|
| `POST` | `/api/v1/logs` | Create log & auto-create/reuse cabinet (supports `application/json` AND `multipart/form-data` with optional `photo`) |
| `GET` | `/api/v1/logs` | List/search/filter/sort logs (`?cabinetId=&cabinetName=&from=&to=&search=&sort=occurredAt_desc&page=1&pageSize=20`) |
| `GET` | `/api/v1/logs/{id}` | Get single log |
| `PUT` | `/api/v1/logs/{id}` | Update cabinet, issue, `occurredAt`, replace `photo` (multipart), or set `removePhoto: true` |
| `DELETE` | `/api/v1/logs/{id}` | Delete log & clean up photo from Storage (queues automatic retry on Storage failure; cabinet is NOT deleted) |
| `GET` | `/api/v1/logs/{id}/photos` | Return short-lived signed URLs for all photos attached to the log |
| `GET` | `/api/v1/logs/{id}/photo-url` | Return a short-lived signed URL for the first attached photo (backwards-compatible) |

**Date/Time & Filter Conventions:**
- `occurredAt`: accepts ISO 8601 values with timezone offset (e.g. `2026-11-23T10:30:00+02:00`); converted to UTC and stored in PostgreSQL `timestamptz`. Defaults to server UTC now when omitted.
- `from`: inclusive (`occurredAt >= from`).
- `to`: exclusive (`occurredAt < to`).
- `sort`: `occurredAt_desc` (default), `occurredAt_asc`, `createdAt_desc`, `createdAt_asc`.

### Excel Reports
| Method | Endpoint | Purpose |
|---|---|---|
| `GET` | `/api/v1/reports/cabinets/{cabinetId}/excel` | Download `.xlsx` report for one cabinet (`?from=&to=&sort=occurredAt_asc`). **Never includes photos, thumbnails, or photo URLs.** |

---

## 5. Project Structure

```text
CabinetLogs.slnx
├── docs/
│   └── Cabinet_Logs_Backend_Specification.md
├── src/
│   ├── CabinetLogs.Domain/
│   │   ├── Common/CabinetNameNormalizer.cs
│   │   ├── Entities/ (Cabinet, ClientDevice, MaintenanceLog, PendingStorageCleanup)
│   │   └── Enums/DevicePlatform.cs
│   ├── CabinetLogs.Application/
│   │   ├── Common/ (PagedResult, PhotoValidator, Exceptions, Options)
│   │   ├── DTOs/ (ClientDevices, Cabinets, Logs, Reports)
│   │   ├── Interfaces/
│   │   └── Services/ (ClientDeviceService, CabinetService, LogService, ReportAppService)
│   ├── CabinetLogs.Infrastructure/
│   │   ├── Persistence/ (CabinetLogsDbContext, Configurations, Migrations, Sql/supabase_init.sql)
│   │   ├── Storage/ (SupabaseStorageService, StorageCleanupBackgroundService)
│   │   └── Reports/ (ClosedXmlExcelReportService)
│   └── CabinetLogs.Api/
│       ├── Controllers/ (ClientDevices, Cabinets, Logs, Reports, Health)
│       ├── Extensions/OpenApiExtensions.cs
│       ├── Middleware/ (GlobalExceptionHandler, OptionalApiKeyMiddleware)
│       ├── Models/LogFormRequests.cs
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       └── appsettings.example.json
└── tests/
    └── CabinetLogs.Tests/
```

---

## 6. Plugging In Your Final Excel Template Later

Excel workbook generation is completely isolated inside [`src/CabinetLogs.Infrastructure/Reports/ClosedXmlExcelReportService.cs`](src/CabinetLogs.Infrastructure/Reports/ClosedXmlExcelReportService.cs) (implementing `IExcelReportService`).

When your final designed `.xlsx` template is ready:
1. Place the template file in a folder copied to output (e.g. `src/CabinetLogs.Infrastructure/Reports/Templates/CabinetLogsTemplate.xlsx`).
2. Update `ClosedXmlExcelReportService.GenerateCabinetReportAsync` to open the template via `new XLWorkbook(templatePath)` and write `reportData` fields into the target cells — no changes are required in controllers or `LogService`.

---

## 7. Deploy to GitHub & Render (Docker)

This repository includes a production-ready [`Dockerfile`](Dockerfile), [`.dockerignore`](.dockerignore), and [`render.yaml`](render.yaml) configured for **Render Web Services**.

### Step 1: Push to GitHub (Safely — No Secrets Included)

Your local credentials stay in `src/CabinetLogs.Api/appsettings.Local.json` (which is ignored by `.gitignore` and `.dockerignore`).

```powershell
git init -b main
git add .
git status   # Confirm appsettings.Local.json is NOT listed!
git commit -m "Initial Cabinet Logs .NET 10 backend"
git remote add origin https://github.com/<YOUR_USERNAME>/<YOUR_REPO>.git
git push -u origin main
```

### Step 2: Deploy on Render

1. Go to [Render Dashboard](https://dashboard.render.com) → **New → Web Service** (or **New → Blueprint** to auto-import `render.yaml`).
2. Connect your GitHub repository and select **Runtime: Docker** (Dockerfile path: `./Dockerfile`).
3. Set **Health Check Path** to `/api/v1/health`.
4. Add these **Environment Variables** in Render:

| Key | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | Your Supabase **Session Pooler** connection string *(see IPv4 note below)* |
| `Supabase__Url` | `https://<YOUR_PROJECT_REF>.supabase.co` |
| `Supabase__ServiceRoleKey` | Your Supabase `service_role` key |
| `Supabase__AnonKey` | Your Supabase `anon` key *(optional)* |
| `Supabase__StorageBucket` | `cabinet-log-photos` |
| `Supabase__SignedUrlExpirySeconds` | `300` |
| `PhotoUpload__MaxFileSizeBytes` | `5242880` |
| `PhotoUpload__MaxPhotosPerLog` | `10` |
| `Scalar__Enabled` | `true` *(or `false` when you want to hide docs)* |
| `ApiSecurity__RequireApiKey` | `true` *(recommended on public internet!)* |
| `ApiSecurity__ApiKeyHeaderName` | `X-Api-Key` |
| `ApiSecurity__ApiKey` | A strong random key *(then enter the same key in Flutter Settings)* |
| `Cors__AllowedOrigins__0` | `*` |

> [!IMPORTANT]
> **Supabase IPv4 Note on Render:** Render's outbound network uses **IPv4**, while Supabase's Direct DB host (`db.<ref>.supabase.co`) is IPv6-only on newer projects. On Render, use Supabase's **Session Pooler** connection string (`Host=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<PROJECT_REF>;Password=<DB_PASSWORD>;SSL Mode=Require;Trust Server Certificate=true`).

### Step 3: Connect Your Flutter App

Once Render finishes deploying, open your Flutter app → **Settings** → set **API Base URL** to `https://<your-service>.onrender.com` (and enter `X-Api-Key` if enabled) → click **Test Connection**.

---

## 8. Running Tests

Run the full automated test suite (no external Supabase credentials required — uses an in-memory SQLite test database and a fake storage provider):

```powershell
dotnet test CabinetLogs.slnx
```
