# Cabinet Logs Backend — .NET + Supabase

## 1. Project overview

Build a REST API using **ASP.NET Core Web API (.NET)** with **Supabase** as the backend platform.

The system stores maintenance/issue logs for cabinets and other equipment. It will later be consumed by:
- Flutter Desktop application
- Flutter Mobile application

The first delivery is the backend API and database/storage integration. The Flutter applications and final Excel report layout will be implemented later.

### Main workflow

1. On first launch, the application can ask the user to enter a name for the current client device (for example, `Ahmed's PC` or `Maintenance Tablet`).
2. The user creates a log by entering:
   - Cabinet/device name
   - Issue or log description
   - Date and time
   - Optional photo
3. The backend saves the log.
4. If the cabinet/device name does not already exist, the backend creates it automatically.
5. The cabinet/device becomes available as a category/filter.
6. The user can open a cabinet/device and view or search all its logs.
7. The user can edit or delete logs.
8. The user can request an Excel report for one selected cabinet/device and a date/time range. The report will contain that cabinet's logs and will use a designed Excel template that will be supplied/implemented later.

## 2. Terminology and data model

To avoid mixing the computer/phone running the app with the equipment being reported:

- **Client device:** The PC, laptop, or mobile phone on which the Flutter app is installed. Example: `Ahmed's Laptop`.
- **Cabinet:** The equipment being logged. Example: `LAF-01`, `BSC-02`, or `Fume Hood`.
- **Log:** One issue/maintenance record belonging to a cabinet.

The cabinet list is the category list. It is created automatically from the cabinet name entered when creating a log. There is no need for the user to create a cabinet category separately in the first version.

A cabinet should have one canonical record. When a new log is submitted, the backend should find an existing cabinet by normalized name; if none exists, create it. This prevents duplicate categories caused only by letter casing or extra spaces.

## 3. Functional requirements

### 3.1 Client device and log creator

- **There is no login or user account in version 1.**
- On first launch, the app asks the user to enter a name for the current PC/mobile device (for example, `Ahmed's PC` or `Maintenance Tablet`).
- Save this name locally in the Flutter application so it is not requested every time.
- Register the installation/device with the backend and associate its ID with logs created from that installation.
- Store a platform/type if available: `Windows`, `macOS`, `Android`, `iOS`, or `Other`.
- Every log must show the name of the PC/mobile device that created it. This is the **created by / source device name**, not the cabinet name.
- The device name can be changed later. Decide whether changing it should update the displayed name on historical logs; recommended: preserve the name as it was when each log was created by storing a snapshot on the log.
- Since there is no authentication, the submitted device name is informational metadata only. It does not prove the identity of the person or device and must not be treated as a security credential.

### 3.2 Cabinets

- Cabinets are created automatically when the first log is saved for a new cabinet name.
- List all cabinets.
- Get one cabinet by ID.
- Search cabinets by name.
- Show the number of logs for each cabinet.
- Get all logs belonging to a cabinet.
- Do not automatically delete a cabinet when its last log is deleted. Cabinet deletion/archiving can be added as an administrative feature later.

### 3.3 Logs

Each log contains:

| Field | Required | Description |
|---|---:|---|
| `id` | Yes | Unique UUID |
| `cabinetId` | Yes | Cabinet/category reference |
| `issue` | Yes | Issue description or log details |
| `occurredAt` | Yes | Date and time of the issue; defaults to current time |
| `photoPath` | No | Supabase Storage object path |
| `clientDeviceId` | No | Registered PC/mobile installation that submitted the log |
| `createdByDeviceName` | Yes | Snapshot of the device name at the time the log was created |
| `createdAt` | Yes | When the record was created by the backend |
| `updatedAt` | Yes | When the record was last updated |

Requirements:
- Date/time input is optional in the create request. If omitted, the backend uses the current UTC time.
- If the user supplies a date/time, preserve that exact moment. The API should accept ISO 8601 date-time values with timezone offset. Store timestamps as `timestamptz` in PostgreSQL.
- Display dates in the Flutter app using the user's local timezone.
- A photo is optional. Support common image formats such as JPEG, PNG, and WebP.
- Validate file type and file size on the server.
- A log can be created without a photo.
- Editing a log should allow changing the cabinet, issue, date/time, and photo.
- Deleting a log should also remove its associated photo from Storage when possible. If Storage deletion fails, record/log the failure and allow cleanup to be retried.
- Use pagination for log listing; do not return every log in one response.

### 3.4 Search and filtering

Support:
- Search logs by text in the issue description.
- Filter by cabinet ID.
- Filter by cabinet name.
- Filter by date/time range (`from` and `to`).
- Sort by issue date/time, newest first by default.
- Combine filters, for example: cabinet + date range + search text.
- Paginate results using `page` and `pageSize`.

Search should be case-insensitive where practical.

### 3.5 Excel report

The user selects one cabinet and optionally a date/time range, then requests an Excel report.

The report must:
- Contain only logs for the selected cabinet.
- Include the cabinet name in the report title/header.
- Include the log date and time.
- Include the issue/details.
- **Do not include photos in the Excel report.**
- Do not embed images, photo thumbnails, or photo URLs in the workbook.
- Sort logs chronologically, with the default order to be confirmed when the template is designed.
- Use the final designed Excel template, which will be provided later.
- Be generated by the backend and returned as an `.xlsx` download.

The Excel template and exact column positions, styling, logo, print settings, and image placement are **not defined yet**. Keep report generation isolated in a service so the template can be added without changing the log CRUD API.

Recommended .NET library: `ClosedXML` for creating/editing `.xlsx` files. If the final template requires embedding images, verify the chosen library/version supports the required image handling.

## 4. Suggested database schema (Supabase PostgreSQL)

Use UUID primary keys and UTC-aware timestamps.

### 4.1 `cabinets`

| Column | Type | Rules |
|---|---|---|
| `id` | `uuid` | Primary key, default `gen_random_uuid()` |
| `name` | `text` | Required |
| `normalized_name` | `text` | Required, unique |
| `created_at` | `timestamptz` | Required, default `now()` |
| `updated_at` | `timestamptz` | Required, default `now()` |

`normalized_name` should be generated by backend normalization (trim whitespace and use a consistent case-insensitive representation). Add a unique constraint/index to prevent duplicate cabinets.

### 4.2 `client_devices`

| Column | Type | Rules |
|---|---|---|
| `id` | `uuid` | Primary key, default `gen_random_uuid()` |
| `name` | `text` | Required |
| `platform` | `text` | Optional |
| `installation_key` | `text` | Optional; unique if used |
| `created_at` | `timestamptz` | Required, default `now()` |
| `updated_at` | `timestamptz` | Required, default `now()` |

`installation_key` is an app-generated installation identifier, not a secret and not a replacement for authentication. Do not use hardware serial numbers or other sensitive identifiers unless there is a clear requirement.

### 4.3 `logs`

| Column | Type | Rules |
|---|---|---|
| `id` | `uuid` | Primary key, default `gen_random_uuid()` |
| `cabinet_id` | `uuid` | Required, FK to `cabinets.id` |
| `issue` | `text` | Required |
| `occurred_at` | `timestamptz` | Required |
| `photo_path` | `text` | Nullable |
| `client_device_id` | `uuid` | Nullable, FK to `client_devices.id` |
| `created_by_device_name` | `text` | Required; snapshot of the submitting device name |
| `created_at` | `timestamptz` | Required, default `now()` |
| `updated_at` | `timestamptz` | Required, default `now()` |

Recommended indexes:
- `logs(cabinet_id, occurred_at DESC)`
- `logs(occurred_at DESC)`
- `cabinets(normalized_name)` unique
- Add a full-text search index for `issue` if log volume/search requirements justify it.

Use `ON DELETE RESTRICT` for cabinet-to-log relationships in the first version to prevent accidental deletion of a cabinet that still has logs. Decide explicitly whether client-device deletion should set `client_device_id` to null.

### 4.4 Relationships

- One cabinet has many logs.
- One client device can submit many logs.
- Each log belongs to exactly one cabinet.
- A log may optionally reference one client device.
- Each log may have zero or one photo in the first version.

## 5. Photo storage

Use a Supabase Storage bucket, for example:

`cabinet-log-photos`

Suggested object path:

`logs/{logId}/{fileName}`

Requirements:
- Keep the bucket private by default if logs/photos should not be publicly accessible.
- Store the object path in `logs.photo_path`, not a temporary signed URL.
- Generate short-lived signed URLs when the Flutter app needs to display/download a private photo.
- Validate MIME type and maximum file size in the API.
- Use generated file names rather than trusting the original filename.
- Do not store image binary data directly in PostgreSQL.
- When replacing a photo, upload the new file and update the record safely, then delete the old object.
- Define a cleanup/retry strategy for orphaned files if a database update or Storage operation fails.

The maximum upload size should be configurable. Choose the initial limit before implementation (for example, 5 MB) and enforce it consistently in the API and client.

## 6. Authentication, shared access, and security

### Version 1 access rules

- **No login, registration, username, password, or user accounts.**
- All app installations share the same data.
- Every user can view all cabinets and logs.
- Every user can create, edit, and delete logs.
- There is no per-user or per-device data isolation.
- The device name is saved with each log to indicate which named PC/mobile installation created it. It is not authentication and can be changed or imitated.

### Security implications

Because there is no login, anyone who can reach the API may be able to read, create, edit, or delete all records. This is acceptable only if the API is intentionally limited to a trusted environment or protected by another access mechanism.

Before public deployment, protect the API using an appropriate network restriction or shared application credential/API-key mechanism. Do not embed a privileged Supabase `service_role` key in Flutter. Keep Supabase secrets on the server and perform database and Storage operations through the .NET API.

If using Supabase Row Level Security:
- Do not create policies that accidentally expose the database publicly.
- Keep privileged database access on the server.
- The API must enforce the intended shared read/write rules.
- Revisit authentication and authorization before exposing the service to the public internet.

No tenant/owner column is required for version 1 because all users share the same data.

## 7. API design

Base route:

`/api/v1`

Use DTOs for requests and responses. Do not expose database entities directly.

### 7.1 Client device endpoints

| Method | Endpoint | Purpose |
|---|---|---|
| `POST` | `/client-devices` | Register a PC/mobile installation |
| `GET` | `/client-devices` | List registered client devices |
| `GET` | `/client-devices/{id}` | Get one client device |
| `PUT` | `/client-devices/{id}` | Update its name/platform |
| `DELETE` | `/client-devices/{id}` | Delete/unregister it, subject to relationship rules |

Example create request:

```json
{
  "name": "Workshop PC",
  "platform": "Windows",
  "installationKey": "generated-installation-guid"
}
```

### 7.2 Cabinet endpoints

| Method | Endpoint | Purpose |
|---|---|---|
| `GET` | `/cabinets` | List/search cabinets and log counts |
| `GET` | `/cabinets/{id}` | Get cabinet details |
| `GET` | `/cabinets/{id}/logs` | Get paginated logs for a cabinet |

Example query:

`GET /cabinets?search=LAF&page=1&pageSize=20`

Cabinets are created automatically through log creation. A manual cabinet create endpoint is not required for the first version. A rename endpoint can be added later if needed.

### 7.3 Log endpoints

| Method | Endpoint | Purpose |
|---|---|---|
| `POST` | `/logs` | Create a log, automatically creating the cabinet if needed |
| `GET` | `/logs` | List/search/filter logs |
| `GET` | `/logs/{id}` | Get one log |
| `PUT` | `/logs/{id}` | Update a log |
| `DELETE` | `/logs/{id}` | Delete a log and attempt photo cleanup |

Example create request (without a photo):

```json
{
  "cabinetName": "LAF-01",
  "issue": "The temperature display is not responding.",
  "occurredAt": "2026-11-23T10:30:00+02:00",
  "clientDeviceId": "00000000-0000-0000-0000-000000000000"
}
```

`occurredAt` and `clientDeviceId` may be omitted when not available. If `occurredAt` is omitted, the server uses the current UTC time. The sample UUID above is illustrative only; clients must send a real registered ID or omit the field. The backend must resolve the device name from the registered device when possible and save a snapshot in `createdByDeviceName` on the log. If device registration is intentionally skipped, accept a validated `createdByDeviceName` value instead.

For photo upload, use `multipart/form-data` with fields:
- `cabinetName`
- `issue`
- `occurredAt` (optional)
- `clientDeviceId` (optional)
- `photo` (optional file)

This keeps file upload straightforward. The API can also support JSON-only requests when no photo is attached.

Example log search:

`GET /logs?cabinetId={uuid}&from=2026-11-01T00:00:00Z&to=2026-11-30T23:59:59Z&search=temperature&page=1&pageSize=20&sort=occurredAt_desc`

Define whether `to` is inclusive or exclusive; recommended convention is an exclusive upper bound (`occurredAt < to`) to make date ranges predictable.

### 7.4 Photo endpoint

| Method | Endpoint | Purpose |
|---|---|---|
| `GET` | `/logs/{id}/photo-url` | Return a short-lived signed URL if a photo exists |

Do not return permanent public URLs for private photos.

### 7.5 Report endpoint

| Method | Endpoint | Purpose |
|---|---|---|
| `GET` | `/reports/cabinets/{cabinetId}/excel` | Generate/download Excel report |

Suggested query:

`GET /reports/cabinets/{cabinetId}/excel?from=2026-11-01T00:00:00Z&to=2026-12-01T00:00:00Z`

The date range is optional. If omitted, export all logs for that cabinet.

Return:
- HTTP `200 OK`
- Content-Type: `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- A meaningful filename, for example `LAF-01_Logs_2026-11-23.xlsx`

## 8. Example response shapes

### Log response

```json
{
  "id": "uuid",
  "cabinet": {
    "id": "uuid",
    "name": "LAF-01"
  },
  "issue": "The temperature display is not responding.",
  "occurredAt": "2026-11-23T08:30:00Z",
  "photoAvailable": true,
  "clientDevice": {
    "id": "uuid",
    "name": "Workshop PC",
    "platform": "Windows"
  },
  "createdAt": "2026-11-23T08:35:00Z",
  "updatedAt": "2026-11-23T08:35:00Z"
}
```

The response may include a signed photo URL only when requested through the photo URL endpoint. Avoid generating many signed URLs unnecessarily in a large paginated response.

### Paginated response

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0
}
```

## 9. Recommended .NET architecture

Use a maintainable layered structure. Keep the first implementation focused; do not introduce unnecessary microservices.

Suggested solution:

```text
CabinetLogs/
├── CabinetLogs.sln
├── src/
│   ├── CabinetLogs.Api/
│   │   ├── Controllers/
│   │   ├── Middleware/
│   │   ├── Extensions/
│   │   ├── Program.cs
│   │   └── appsettings.json
│   ├── CabinetLogs.Application/
│   │   ├── DTOs/
│   │   ├── Interfaces/
│   │   ├── Services/
│   │   ├── Validators/
│   │   └── Common/
│   ├── CabinetLogs.Domain/
│   │   ├── Entities/
│   │   └── Enums/
│   └── CabinetLogs.Infrastructure/
│       ├── Persistence/
│       ├── Supabase/
│       ├── Storage/
│       └── Reports/
└── README.md
```

Suggested responsibilities:
- **Api:** HTTP controllers, request binding, authentication configuration, exception handling, Swagger/OpenAPI.
- **Application:** use cases, DTOs, validation, service interfaces.
- **Domain:** core entities and business rules.
- **Infrastructure:** PostgreSQL/Supabase access, Storage uploads, signed URLs, Excel generation.

Use dependency injection and asynchronous I/O. Keep controllers thin.

### Data access choice

Two reasonable approaches:
1. **EF Core with PostgreSQL provider (`Npgsql.EntityFrameworkCore.PostgreSQL`)** — recommended for migrations, relationships, LINQ queries, and maintainable CRUD.
2. Supabase client/PostgREST — useful if intentionally using Supabase APIs, but complex filtering and transaction handling may be less convenient for this application.

Recommended starting point: connect .NET directly to Supabase's PostgreSQL database using Npgsql/EF Core, and use Supabase Storage APIs for photos. Keep the database connection string server-side. Use the Supabase connection method/port recommended for the deployment environment (direct connection or pooler as appropriate).

## 10. Validation and error handling

Validate on the backend even if Flutter also validates.

Suggested rules:
- `cabinetName`: required, trimmed, reasonable maximum length (for example 150 characters).
- `issue`: required, non-empty, reasonable maximum length (for example 10,000 characters).
- `occurredAt`: valid ISO 8601 date/time; reject invalid values.
- `clientDeviceId`: if supplied, it must reference an existing client device accessible to the caller.
- `photo`: optional; allow only configured image MIME types and enforce configured size limit.
- `page`: minimum 1.
- `pageSize`: default 20 and enforce a maximum (for example 100).
- `from` must be earlier than `to`.

Use consistent HTTP status codes:
- `200 OK` — successful read/update.
- `201 Created` — successful creation.
- `204 No Content` — successful deletion.
- `400 Bad Request` — invalid input.
- `401 Unauthorized` — missing/invalid authentication, when authentication is enabled.
- `403 Forbidden` — authenticated but not allowed.
- `404 Not Found` — record does not exist.
- `409 Conflict` — duplicate or conflicting operation where appropriate.
- `413 Payload Too Large` — photo exceeds upload limit.
- `415 Unsupported Media Type` — unsupported file type.
- `500 Internal Server Error` — unexpected server error.

Return a consistent error structure, preferably ASP.NET Core `ProblemDetails`. Never return database credentials, service-role keys, stack traces, or internal exception details to clients.

## 11. Important implementation details

### Automatic cabinet creation

When creating a log:
1. Trim the supplied cabinet name.
2. Normalize it consistently (case-insensitive comparison).
3. Find the cabinet by `normalized_name`.
4. If found, use its ID.
5. If not found, create it.
6. Create the log referencing that cabinet.
7. Ensure concurrent requests cannot create duplicate cabinets; rely on a unique database constraint and handle a uniqueness conflict by retrieving the existing cabinet.

The cabinet and log creation should be transactional where possible.

### Updating a log's cabinet

If the user changes the cabinet name during log update:
1. Find or create the target cabinet using the same normalization logic.
2. Update `cabinet_id`.
3. Do not create a duplicate cabinet if it already exists.

### Time handling

- Store all timestamps as `timestamptz`.
- Use UTC internally.
- Accept ISO 8601 values with offsets from Flutter.
- If the date/time is omitted on creation, use server UTC now.
- Keep `occurred_at` (when the issue happened) separate from `created_at` (when the record was saved). These values can differ because the user may enter an old issue manually.

### Excel generation

Implement a dedicated `IExcelReportService` (or equivalent). It should receive a cabinet and its filtered logs, then generate the workbook. Do not put Excel formatting code in controllers or log services.

## 12. Configuration and secrets

Use environment variables or a secure secrets manager for:
- Supabase project URL
- Supabase anon key (only if required by the server integration)
- Supabase service-role key (server only; never Flutter)
- PostgreSQL connection string
- Storage bucket name
- Maximum photo upload size
- Signed URL expiry duration
- Authentication/JWT validation settings, if applicable

Do not commit real keys or connection strings to Git. Provide an `appsettings.example.json` with placeholder values only.

## 13. Testing plan

Test the API with Swagger and Postman before building the Flutter applications.

Minimum scenarios:
1. Register a Windows client device.
2. Register an Android client device.
3. Create a log for a new cabinet; verify the cabinet is created automatically.
4. Create another log for the same cabinet with different letter casing; verify no duplicate cabinet is created.
5. Create a log without a photo.
6. Create a log with a valid photo.
7. Omit `occurredAt`; verify it defaults to current time.
8. Supply a manual date/time with timezone offset; verify the stored instant is correct.
9. List cabinets and verify log counts.
10. Search cabinets by name.
11. List logs with pagination.
12. Filter logs by cabinet.
13. Filter logs by date range.
14. Search issue text.
15. Combine cabinet, date range, and text filters.
16. Get one log by ID.
17. Update issue/date/cabinet.
18. Replace or remove a photo.
19. Delete a log and verify photo cleanup behavior.
20. Request an Excel report for one cabinet.
21. Request a report with a date range.
22. Verify empty results produce a valid report or a clear documented response.
23. Verify invalid IDs, dates, files, and oversized uploads return appropriate errors.
24. Verify unauthorized users cannot access data if authentication is enabled.

## 14. Suggested implementation phases

### Phase 1 — Project and database
- Create the .NET solution and projects.
- Configure Supabase PostgreSQL connection.
- Create entities and EF Core DbContext.
- Create and apply the initial database migration.
- Verify database health/connection.

### Phase 2 — Client devices and cabinets
- Implement client-device registration/update.
- Implement cabinet listing/search.
- Implement automatic cabinet creation logic.

### Phase 3 — Log CRUD
- Implement create/list/get/update/delete.
- Implement pagination, text search, cabinet filtering, and date filtering.
- Test all endpoints with Postman.

### Phase 4 — Photo storage
- Create the Supabase Storage bucket.
- Implement upload, replacement, deletion, and signed URL generation.
- Test failure and cleanup scenarios.

### Phase 5 — Excel report
- Implement report endpoint and service.
- Initially generate a basic workbook for testing.
- Integrate the final designed Excel template when it is provided.

### Phase 6 — Security and deployment
- Configure the no-login shared-access model safely.
- Configure RLS/server-side database access so privileged credentials are never exposed to Flutter.
- Configure production environment variables.
- Deploy API and test from an external network.
- Enable HTTPS and review logging/backup strategy.

## 15. Decisions to confirm before implementation

These points are intentionally left open rather than silently assuming requirements:

1. **Authentication:** Will users sign in, or will the first version be a shared internal system? Recommended: authentication if the API is publicly reachable.
2. **Data visibility:** Can every user see and edit all cabinets/logs, or should records be separated by company/customer?
3. **Client device registration:** Should the app ask for a PC/mobile name once and save it locally, then register it with the backend?
4. **Cabinet naming:** Should two names that differ only by capitalization/spaces refer to the same cabinet? Recommended: yes.
5. **Photo limit:** Confirm the maximum upload size. Suggested initial limit: 5 MB.
6. **Report layout:** The designed Excel template will be supplied later. Confirm whether photos must be embedded in Excel or represented by a link/reference.
7. **Cabinet management:** Should cabinets be renameable/archivable, or should they only be created automatically from logs in version 1?

## 16. Out of scope for the first backend version

Unless added later, do not include:
- Flutter UI implementation.
- Notifications.
- User accounts, login, and role-based access control.
- Cabinet maintenance schedules.
- Spare-parts or inventory management.
- Comments or multi-user discussions on a log.
- Multiple photos per log.
- Final Excel styling before the template is supplied.

---

## Definition of done

The backend first version is ready when:
- The database is hosted on Supabase PostgreSQL.
- The .NET API can register client devices.
- Saving a log automatically creates/reuses its cabinet category.
- Log CRUD works.
- Search, filtering, and pagination work.
- Optional photo upload and retrieval work through Supabase Storage.
- A selected cabinet's logs can be exported to `.xlsx`.
- Swagger/Postman tests pass.
- Secrets are not exposed to Flutter clients or committed to source control.
- Shared access works as specified: all users can view/edit the common data.
- Each log records the name of the PC/mobile device that created it.
- No login is required in version 1.
- Excel reports contain no embedded photos or photo links.
- The no-login API is protected appropriately before public production use.
