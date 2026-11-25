using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CabinetLogs.Application.Common;
using CabinetLogs.Application.DTOs.Cabinets;
using CabinetLogs.Application.DTOs.ClientDevices;
using CabinetLogs.Application.DTOs.Logs;
using CabinetLogs.Infrastructure.Persistence;
using CabinetLogs.Infrastructure.Reports;
using CabinetLogs.Tests.Infrastructure;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CabinetLogs.Tests;

public sealed class CabinetLogsApiIntegrationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Valid minimal 1x1 PNG bytes (magic header: 89 50 4E 47 0D 0A 1A 0A)
    private static readonly byte[] ValidPngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
        0xDE
    ];

    private CabinetLogsWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new CabinetLogsWebApplicationFactory();
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Health_And_Scalar_OpenApi_Endpoints_Work()
    {
        var healthResponse = await _client.GetAsync("/api/v1/health");
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);

        var openApiResponse = await _client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
        var openApiJson = await openApiResponse.Content.ReadAsStringAsync();
        Assert.Contains("Cabinet Logs API", openApiJson);
        Assert.Contains("multipart/form-data", openApiJson);
        Assert.Contains("application/json", openApiJson);

        var scalarResponse = await _client.GetAsync("/scalar/v1");
        Assert.Equal(HttpStatusCode.OK, scalarResponse.StatusCode);
    }

    [Fact]
    public async Task ClientDevice_Lifecycle_Preserves_Log_Snapshot_On_Rename_And_Delete()
    {
        // 1. Register Windows device
        var createWin = await _client.PostAsJsonAsync("/api/v1/client-devices", new
        {
            name = "Workshop PC",
            platform = "Windows",
            installationKey = "win-install-001"
        });
        Assert.Equal(HttpStatusCode.Created, createWin.StatusCode);
        var winDevice = await createWin.Content.ReadFromJsonAsync<ClientDeviceDto>(JsonOptions);
        Assert.NotNull(winDevice);
        Assert.Equal("Windows", winDevice.Platform);

        // Duplicate installationKey -> 409 Conflict
        var duplicate = await _client.PostAsJsonAsync("/api/v1/client-devices", new
        {
            name = "Duplicate PC",
            platform = "Windows",
            installationKey = "win-install-001"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        // 2. Register Android device
        var createAndroid = await _client.PostAsJsonAsync("/api/v1/client-devices", new
        {
            name = "Maintenance Tablet",
            platform = "Android",
            installationKey = "android-install-001"
        });
        Assert.Equal(HttpStatusCode.Created, createAndroid.StatusCode);

        // 3. Create log from Windows device
        var logResp = await _client.PostAsJsonAsync("/api/v1/logs", new
        {
            cabinetName = "LAF-01",
            issue = "Airflow sensor fluctuating",
            clientDeviceId = winDevice.Id
        });
        Assert.Equal(HttpStatusCode.Created, logResp.StatusCode);
        var createdLog = await logResp.Content.ReadFromJsonAsync<LogDto>(JsonOptions);
        Assert.NotNull(createdLog);
        Assert.Equal("Workshop PC", createdLog.CreatedByDeviceName);

        // 4. Rename Windows device -> historical log keeps snapshot "Workshop PC"
        var renameResp = await _client.PutAsJsonAsync($"/api/v1/client-devices/{winDevice.Id}", new
        {
            name = "Lab Desk PC",
            platform = "Windows"
        });
        Assert.Equal(HttpStatusCode.OK, renameResp.StatusCode);

        var fetchedLogAfterRename = await _client.GetFromJsonAsync<LogDto>($"/api/v1/logs/{createdLog.Id}", JsonOptions);
        Assert.NotNull(fetchedLogAfterRename);
        Assert.Equal("Workshop PC", fetchedLogAfterRename.CreatedByDeviceName);

        // 5. Delete device -> log remains with clientDevice == null and snapshot preserved
        var deleteDeviceResp = await _client.DeleteAsync($"/api/v1/client-devices/{winDevice.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteDeviceResp.StatusCode);

        var fetchedLogAfterDelete = await _client.GetFromJsonAsync<LogDto>($"/api/v1/logs/{createdLog.Id}", JsonOptions);
        Assert.NotNull(fetchedLogAfterDelete);
        Assert.Null(fetchedLogAfterDelete.ClientDevice);
        Assert.Equal("Workshop PC", fetchedLogAfterDelete.CreatedByDeviceName);
    }

    [Fact]
    public async Task AutomaticCabinetCreation_DeduplicatesByCaseAndSpaces_AndSupportsFilteringAndPagination()
    {
        // Create log 1 with timezone offset +02:00
        var resp1 = await _client.PostAsJsonAsync("/api/v1/logs", new
        {
            cabinetName = "LAF-01",
            issue = "The temperature display is not responding.",
            occurredAt = "2026-11-10T10:30:00+02:00",
            createdByDeviceName = "Ahmed's PC"
        });
        Assert.Equal(HttpStatusCode.Created, resp1.StatusCode);
        var log1 = await resp1.Content.ReadFromJsonAsync<LogDto>(JsonOptions);
        Assert.NotNull(log1);
        Assert.Equal(new DateTimeOffset(2026, 11, 10, 8, 30, 0, TimeSpan.Zero), log1.OccurredAt);

        // Create log 2 with different casing & extra spaces -> must reuse same cabinet
        var resp2 = await _client.PostAsJsonAsync("/api/v1/logs", new
        {
            cabinetName = "  laf-01  ",
            issue = "HEPA filter pressure drop warning.",
            occurredAt = "2026-11-15T14:00:00Z",
            createdByDeviceName = "Maintenance Tablet"
        });
        Assert.Equal(HttpStatusCode.Created, resp2.StatusCode);
        var log2 = await resp2.Content.ReadFromJsonAsync<LogDto>(JsonOptions);
        Assert.NotNull(log2);
        Assert.Equal(log1.Cabinet.Id, log2.Cabinet.Id);

        // Create log 3 for another cabinet, omitting occurredAt (defaults to UTC now)
        var beforeCreate = DateTimeOffset.UtcNow.AddSeconds(-5);
        var resp3 = await _client.PostAsJsonAsync("/api/v1/logs", new
        {
            cabinetName = "BSC-02",
            issue = "UV lamp replaced.",
            createdByDeviceName = "Ahmed's PC"
        });
        Assert.Equal(HttpStatusCode.Created, resp3.StatusCode);
        var log3 = await resp3.Content.ReadFromJsonAsync<LogDto>(JsonOptions);
        Assert.NotNull(log3);
        Assert.True(log3.OccurredAt >= beforeCreate);

        // List cabinets -> exactly 2 cabinets, LAF-01 has logCount = 2
        var cabinets = await _client.GetFromJsonAsync<PagedResult<CabinetSummaryDto>>("/api/v1/cabinets?search=laf", JsonOptions);
        Assert.NotNull(cabinets);
        Assert.Equal(1, cabinets.TotalCount);
        Assert.Equal(2, cabinets.Items[0].LogCount);

        // Combined filter: cabinetId + date range + text search (case-insensitive)
        var filtered = await _client.GetFromJsonAsync<PagedResult<LogDto>>(
            $"/api/v1/logs?cabinetId={log1.Cabinet.Id}&from=2026-11-01T00:00:00Z&to=2026-11-12T00:00:00Z&search=TEMPERATURE&page=1&pageSize=10",
            JsonOptions);
        Assert.NotNull(filtered);
        Assert.Equal(1, filtered.TotalCount);
        Assert.Equal(log1.Id, filtered.Items[0].Id);

        // Invalid date range (from >= to) -> 400 Bad Request
        var badRange = await _client.GetAsync("/api/v1/logs?from=2026-11-20T00:00:00Z&to=2026-11-10T00:00:00Z");
        Assert.Equal(HttpStatusCode.BadRequest, badRange.StatusCode);
    }

    [Fact]
    public async Task PhotoUpload_SignedUrl_Replace_Delete_And_CleanupQueue_Work()
    {
        // 1. Create log with valid PNG photo via multipart/form-data
        using var createForm = new MultipartFormDataContent();
        createForm.Add(new StringContent("Fume Hood 1"), "cabinetName");
        createForm.Add(new StringContent("Sash alarm triggered"), "issue");
        createForm.Add(new StringContent("Lab Laptop"), "createdByDeviceName");
        var fileContent = new ByteArrayContent(ValidPngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        createForm.Add(fileContent, "photo", "original_unsafe_name.png");

        var createResp = await _client.PostAsync("/api/v1/logs", createForm);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var log = await createResp.Content.ReadFromJsonAsync<LogDto>(JsonOptions);
        Assert.NotNull(log);
        Assert.True(log.PhotoAvailable);
        Assert.Single(_factory.FakeStorage.StoredFiles);
        var originalPath = _factory.FakeStorage.StoredFiles.Keys.Single();
        Assert.DoesNotContain("original_unsafe_name", originalPath);

        // 2. Get short-lived signed URL
        var signedUrlResp = await _client.GetFromJsonAsync<PhotoUrlResponse>($"/api/v1/logs/{log.Id}/photo-url", JsonOptions);
        Assert.NotNull(signedUrlResp);
        Assert.Contains("test-signed-token", signedUrlResp.Url);
        Assert.Equal(300, signedUrlResp.ExpiresInSeconds);

        // 3. Replace photo via PUT multipart/form-data (removePhoto=true + new photo) -> old photo deleted, new photo stored
        using var updateForm = new MultipartFormDataContent();
        updateForm.Add(new StringContent("Fume Hood 1"), "cabinetName");
        updateForm.Add(new StringContent("Sash alarm fixed"), "issue");
        updateForm.Add(new StringContent("true"), "removePhoto");
        var newFileContent = new ByteArrayContent(ValidPngBytes);
        newFileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        updateForm.Add(newFileContent, "photos", "replacement.png");

        var updateResp = await _client.PutAsync($"/api/v1/logs/{log.Id}", updateForm);
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        Assert.Contains(originalPath, _factory.FakeStorage.DeletedPaths);
        Assert.Single(_factory.FakeStorage.StoredFiles);

        // 4. Simulate failed storage delete during log deletion -> queued in pending_storage_cleanups, cabinet NOT deleted
        _factory.FakeStorage.FailNextDelete = true;
        var deleteResp = await _client.DeleteAsync($"/api/v1/logs/{log.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        // Verify cabinet still exists with 0 logs
        var cabinetAfterDelete = await _client.GetFromJsonAsync<CabinetDetailDto>($"/api/v1/cabinets/{log.Cabinet.Id}", JsonOptions);
        Assert.NotNull(cabinetAfterDelete);
        Assert.Equal(0, cabinetAfterDelete.LogCount);

        // Verify pending cleanup was queued
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CabinetLogsDbContext>();
        var pendingCount = await db.PendingStorageCleanups.CountAsync();
        Assert.Equal(1, pendingCount);
    }

    [Fact]
    public async Task PhotoUpload_Rejects_InvalidType_SpoofedBytes_And_OversizedFiles()
    {
        // Unsupported MIME type -> 415
        using (var form = new MultipartFormDataContent())
        {
            form.Add(new StringContent("LAF-01"), "cabinetName");
            form.Add(new StringContent("Test"), "issue");
            form.Add(new StringContent("PC"), "createdByDeviceName");
            var pdfContent = new ByteArrayContent("%PDF-1.4"u8.ToArray());
            pdfContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(pdfContent, "photo", "doc.pdf");

            var resp = await _client.PostAsync("/api/v1/logs", form);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, resp.StatusCode);
        }

        // Spoofed PNG (plain text bytes with image/png header) -> 415
        using (var form = new MultipartFormDataContent())
        {
            form.Add(new StringContent("LAF-01"), "cabinetName");
            form.Add(new StringContent("Test"), "issue");
            form.Add(new StringContent("PC"), "createdByDeviceName");
            var fakePng = new ByteArrayContent("not actually a png file at all!"u8.ToArray());
            fakePng.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(fakePng, "photo", "fake.png");

            var resp = await _client.PostAsync("/api/v1/logs", form);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, resp.StatusCode);
        }

        // Oversized (> 1 MB test limit) -> 413
        using (var form = new MultipartFormDataContent())
        {
            form.Add(new StringContent("LAF-01"), "cabinetName");
            form.Add(new StringContent("Test"), "issue");
            form.Add(new StringContent("PC"), "createdByDeviceName");
            var bigBytes = new byte[1_200_000];
            Array.Copy(ValidPngBytes, bigBytes, ValidPngBytes.Length);
            var bigContent = new ByteArrayContent(bigBytes);
            bigContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(bigContent, "photo", "huge.png");

            var resp = await _client.PostAsync("/api/v1/logs", form);
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, resp.StatusCode);
        }
    }

    [Fact]
    public async Task ExcelReport_GeneratesValidWorkbook_WithoutPhotosOrUrls_AndHandlesEmptyRange()
    {
        // Create a log WITH a photo so we can prove the Excel report excludes photos & URLs
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("LAF-09"), "cabinetName");
        form.Add(new StringContent("Replaced intake pre-filter"), "issue");
        form.Add(new StringContent("2026-11-20T09:15:00Z"), "occurredAt");
        form.Add(new StringContent("Technician PC"), "createdByDeviceName");
        var img = new ByteArrayContent(ValidPngBytes);
        img.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(img, "photo", "filter.png");

        var createResp = await _client.PostAsync("/api/v1/logs", form);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var createdLog = await createResp.Content.ReadFromJsonAsync<LogDto>(JsonOptions);
        Assert.NotNull(createdLog);

        // Request Excel report for cabinet
        var reportResp = await _client.GetAsync($"/api/v1/reports/cabinets/{createdLog.Cabinet.Id}/excel");
        Assert.Equal(HttpStatusCode.OK, reportResp.StatusCode);
        Assert.Equal(ClosedXmlExcelReportService.ExcelContentType, reportResp.Content.Headers.ContentType?.MediaType);

        var bytes = await reportResp.Content.ReadAsByteArrayAsync();
        using (var ms = new MemoryStream(bytes))
        using (var wb = new XLWorkbook(ms))
        {
            var ws = wb.Worksheet("Cabinet Logs");
            Assert.Empty(ws.Pictures); // Must NOT embed images
            Assert.Contains("LAF-09", ws.Cell("A1").GetString());
            Assert.Equal("Replaced intake pre-filter", ws.Cell("C8").GetString());
            Assert.Equal("Technician PC", ws.Cell("D8").GetString());

            // Ensure no photo URL or storage path appears anywhere in the worksheet
            foreach (var cell in ws.CellsUsed())
            {
                var text = cell.GetString();
                Assert.DoesNotContain("supabase", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("logs/", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("http", text, StringComparison.OrdinalIgnoreCase);
            }
        }

        // Request report for empty date range -> still valid .xlsx with clear notice
        var emptyResp = await _client.GetAsync(
            $"/api/v1/reports/cabinets/{createdLog.Cabinet.Id}/excel?from=2025-01-01T00:00:00Z&to=2025-02-01T00:00:00Z");
        Assert.Equal(HttpStatusCode.OK, emptyResp.StatusCode);
        var emptyBytes = await emptyResp.Content.ReadAsByteArrayAsync();
        using (var ms = new MemoryStream(emptyBytes))
        using (var wb = new XLWorkbook(ms))
        {
            var ws = wb.Worksheet("Cabinet Logs");
            Assert.Contains("No logs found", ws.Cell("A8").GetString());
        }
    }

    [Fact]
    public async Task OptionalApiKeyMiddleware_EnforcesKey_WhenEnabled()
    {
        await using var securedFactory = new CabinetLogsWebApplicationFactory(new Dictionary<string, string?>
        {
            ["ApiSecurity:RequireApiKey"] = "true",
            ["ApiSecurity:ApiKeyHeaderName"] = "X-Api-Key",
            ["ApiSecurity:ApiKey"] = "super-secret-test-key"
        });
        await securedFactory.InitializeAsync();
        using var client = securedFactory.CreateClient();

        // Health endpoint remains accessible
        var health = await client.GetAsync("/api/v1/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        // Missing API key -> 401
        var unauth = await client.GetAsync("/api/v1/cabinets");
        Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode);

        // Valid API key -> 200
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/cabinets");
        req.Headers.Add("X-Api-Key", "super-secret-test-key");
        var authed = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, authed.StatusCode);
    }

    [Fact]
    public async Task DeleteCabinet_HandlesEmpty_ConflictWhenNonEmpty_AndForceDelete()
    {
        // Create log -> creates cabinet
        var createResp = await _client.PostAsJsonAsync("/api/v1/logs", new
        {
            cabinetName = "TEMP-CAB-01",
            issue = "Initial check",
            createdByDeviceName = "PC-1"
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var log = await createResp.Content.ReadFromJsonAsync<LogDto>(JsonOptions);
        Assert.NotNull(log);

        // Deleting non-empty cabinet with default force=false -> 409 Conflict
        var conflictResp = await _client.DeleteAsync($"/api/v1/cabinets/{log.Cabinet.Id}");
        Assert.Equal(HttpStatusCode.Conflict, conflictResp.StatusCode);

        // Deleting with force=true -> 204 No Content and removes cabinet + logs
        var forceResp = await _client.DeleteAsync($"/api/v1/cabinets/{log.Cabinet.Id}?force=true");
        Assert.Equal(HttpStatusCode.NoContent, forceResp.StatusCode);

        var getCabinetAfter = await _client.GetAsync($"/api/v1/cabinets/{log.Cabinet.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getCabinetAfter.StatusCode);
    }

    [Fact]
    public async Task MultiplePhotos_Create_FetchAll_AddAndSelectiveRemove_AndDeleteCleanup_Work()
    {
        // 1. Create log with 2 photos
        using var createForm = new MultipartFormDataContent();
        createForm.Add(new StringContent("MULTI-01"), "cabinetName");
        createForm.Add(new StringContent("Two photos attached"), "issue");
        createForm.Add(new StringContent("Lab PC"), "createdByDeviceName");

        var img1 = new ByteArrayContent(ValidPngBytes);
        img1.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        createForm.Add(img1, "photos", "photo1.png");

        var img2 = new ByteArrayContent(ValidPngBytes);
        img2.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        createForm.Add(img2, "photos", "photo2.png");

        var createResp = await _client.PostAsync("/api/v1/logs", createForm);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<LogDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.True(created.PhotoAvailable);
        Assert.Equal(2, created.PhotoCount);
        Assert.Equal(2, created.Photos.Count);
        Assert.Equal(2, _factory.FakeStorage.StoredFiles.Count);

        // 2. Fetch all signed photo URLs
        var photoUrls = await _client.GetFromJsonAsync<List<LogPhotoUrlDto>>(
            $"/api/v1/logs/{created.Id}/photos", JsonOptions);
        Assert.NotNull(photoUrls);
        Assert.Equal(2, photoUrls.Count);

        // 3. Remove 1st photo selectively and add a 3rd photo -> total stays 2
        using var updateForm = new MultipartFormDataContent();
        updateForm.Add(new StringContent("MULTI-01"), "cabinetName");
        updateForm.Add(new StringContent("Updated with photo 2 and photo 3"), "issue");
        updateForm.Add(new StringContent(created.Photos[0].Id.ToString()), "removePhotoIds");
        var img3 = new ByteArrayContent(ValidPngBytes);
        img3.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        updateForm.Add(img3, "photos", "photo3.png");

        var updateResp = await _client.PutAsync($"/api/v1/logs/{created.Id}", updateForm);
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updated = await updateResp.Content.ReadFromJsonAsync<LogDto>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(2, updated.PhotoCount);
        Assert.DoesNotContain(updated.Photos, p => p.Id == created.Photos[0].Id);
        Assert.Contains(updated.Photos, p => p.Id == created.Photos[1].Id);
        Assert.Equal(2, _factory.FakeStorage.StoredFiles.Count);

        // 4. Delete log -> cleans up both remaining photos from storage
        var deleteResp = await _client.DeleteAsync($"/api/v1/logs/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
        Assert.Empty(_factory.FakeStorage.StoredFiles);
    }
}
