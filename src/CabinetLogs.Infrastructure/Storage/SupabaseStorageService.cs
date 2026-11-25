using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CabinetLogs.Application.Common.Exceptions;
using CabinetLogs.Application.Common.Options;
using CabinetLogs.Application.DTOs.Logs;
using CabinetLogs.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CabinetLogs.Infrastructure.Storage;

public sealed class SupabaseStorageService : IPhotoStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly SupabaseOptions _options;
    private readonly ILogger<SupabaseStorageService> _logger;

    public SupabaseStorageService(
        HttpClient httpClient,
        IOptions<SupabaseOptions> options,
        ILogger<SupabaseStorageService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> UploadAsync(
        Guid logId,
        Stream content,
        string contentType,
        string extension,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var safeExtension = extension.StartsWith('.') ? extension : $".{extension}";
        var fileName = $"{Guid.NewGuid():N}{safeExtension.ToLowerInvariant()}";
        var objectPath = $"logs/{logId:D}/{fileName}";

        var endpoint = BuildObjectEndpoint(objectPath);

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        AddAuthHeaders(request);
        request.Headers.Add("x-upsert", "true");

        var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Content = streamContent;

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Supabase Storage upload failed for '{ObjectPath}' with status {StatusCode}: {Body}",
                    objectPath,
                    (int)response.StatusCode,
                    body);
                throw new ExternalServiceException("Failed to upload photo to storage.");
            }

            return objectPath;
        }
        catch (ExternalServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading photo '{ObjectPath}' to Supabase Storage.", objectPath);
            throw new ExternalServiceException("Failed to upload photo to storage.", ex);
        }
    }

    public async Task<bool> TryDeleteAsync(string objectPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectPath))
        {
            return true;
        }

        if (!IsConfigured())
        {
            _logger.LogWarning(
                "Supabase Storage is not configured; cannot delete object '{ObjectPath}'.",
                objectPath);
            return false;
        }

        var bucket = Uri.EscapeDataString(_options.StorageBucket.Trim());
        var endpoint = $"{GetNormalizedBaseUrl()}/storage/v1/object/{bucket}";

        using var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
        AddAuthHeaders(request);
        var payload = JsonSerializer.Serialize(new { prefixes = new[] { objectPath } });
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            {
                return true;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Supabase Storage delete failed for '{ObjectPath}' with status {StatusCode}: {Body}",
                objectPath,
                (int)response.StatusCode,
                body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception deleting object '{ObjectPath}' from Supabase Storage.", objectPath);
            return false;
        }
    }

    public async Task<PhotoUrlResponse> CreateSignedUrlAsync(
        string objectPath,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var expiresIn = _options.SignedUrlExpirySeconds > 0 ? _options.SignedUrlExpirySeconds : 300;
        var bucket = Uri.EscapeDataString(_options.StorageBucket.Trim());
        var encodedPath = EncodeObjectPath(objectPath);
        var endpoint = $"{GetNormalizedBaseUrl()}/storage/v1/object/sign/{bucket}/{encodedPath}";

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        AddAuthHeaders(request);
        request.Content = JsonContent.Create(new { expiresIn });

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new NotFoundException("The requested photo could not be found in storage.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Supabase Storage sign URL failed for '{ObjectPath}' with status {StatusCode}: {Body}",
                    objectPath,
                    (int)response.StatusCode,
                    body);
                throw new ExternalServiceException("Failed to generate a signed photo URL.");
            }

            var result = await response.Content.ReadFromJsonAsync<SupabaseSignResponse>(JsonOptions, cancellationToken);
            if (result is null || string.IsNullOrWhiteSpace(result.SignedUrl))
            {
                throw new ExternalServiceException("Storage returned an empty signed URL.");
            }

            var absoluteUrl = BuildAbsoluteSignedUrl(result.SignedUrl);
            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
            return new PhotoUrlResponse(absoluteUrl, expiresAt, expiresIn);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating signed URL for '{ObjectPath}'.", objectPath);
            throw new ExternalServiceException("Failed to generate a signed photo URL.", ex);
        }
    }

    private string BuildObjectEndpoint(string objectPath)
    {
        var bucket = Uri.EscapeDataString(_options.StorageBucket.Trim());
        var encodedPath = EncodeObjectPath(objectPath);
        return $"{GetNormalizedBaseUrl()}/storage/v1/object/{bucket}/{encodedPath}";
    }

    private string BuildAbsoluteSignedUrl(string signedUrl)
    {
        if (Uri.TryCreate(signedUrl, UriKind.Absolute, out _))
        {
            return signedUrl;
        }

        var baseUrl = GetNormalizedBaseUrl();
        if (signedUrl.StartsWith("/storage/v1/", StringComparison.OrdinalIgnoreCase))
        {
            return $"{baseUrl}{signedUrl}";
        }

        if (signedUrl.StartsWith('/'))
        {
            return $"{baseUrl}/storage/v1{signedUrl}";
        }

        return $"{baseUrl}/storage/v1/{signedUrl}";
    }

    private string GetNormalizedBaseUrl() => _options.Url.Trim().TrimEnd('/');

    private static string EncodeObjectPath(string objectPath) =>
        string.Join('/', objectPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));

    private void AddAuthHeaders(HttpRequestMessage request)
    {
        var key = _options.ServiceRoleKey.Trim();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.Add("apikey", key);
    }

    private bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(_options.Url)
            && !_options.Url.Contains("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(_options.ServiceRoleKey)
            && !_options.ServiceRoleKey.Contains("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(_options.StorageBucket);
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured())
        {
            throw new ExternalServiceException(
                "Supabase Storage is not configured. Set Supabase:Url, Supabase:ServiceRoleKey, and Supabase:StorageBucket in configuration.");
        }
    }

    private sealed class SupabaseSignResponse
    {
        [JsonPropertyName("signedURL")]
        public string? SignedUrl { get; set; }
    }
}
