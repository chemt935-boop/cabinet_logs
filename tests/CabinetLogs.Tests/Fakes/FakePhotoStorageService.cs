using CabinetLogs.Application.DTOs.Logs;
using CabinetLogs.Application.Interfaces;

namespace CabinetLogs.Tests.Fakes;

public sealed class FakePhotoStorageService : IPhotoStorageService
{
    public Dictionary<string, (byte[] Bytes, string ContentType)> StoredFiles { get; } = new();
    public List<string> DeletedPaths { get; } = [];
    public bool FailNextDelete { get; set; }

    public async Task<string> UploadAsync(
        Guid logId,
        Stream content,
        string contentType,
        string extension,
        CancellationToken cancellationToken = default)
    {
        var safeExt = extension.StartsWith('.') ? extension : $".{extension}";
        var path = $"logs/{logId:D}/{Guid.NewGuid():N}{safeExt}";

        using var ms = new MemoryStream();
        if (content.CanSeek)
        {
            content.Position = 0;
        }
        await content.CopyToAsync(ms, cancellationToken);
        StoredFiles[path] = (ms.ToArray(), contentType);
        return path;
    }

    public Task<bool> TryDeleteAsync(string objectPath, CancellationToken cancellationToken = default)
    {
        if (FailNextDelete)
        {
            FailNextDelete = false;
            return Task.FromResult(false);
        }

        StoredFiles.Remove(objectPath);
        DeletedPaths.Add(objectPath);
        return Task.FromResult(true);
    }

    public Task<PhotoUrlResponse> CreateSignedUrlAsync(string objectPath, CancellationToken cancellationToken = default)
    {
        var expiresIn = 300;
        var url = $"https://example.supabase.co/storage/v1/object/sign/cabinet-log-photos/{objectPath}?token=test-signed-token";
        return Task.FromResult(new PhotoUrlResponse(url, DateTimeOffset.UtcNow.AddSeconds(expiresIn), expiresIn));
    }
}
