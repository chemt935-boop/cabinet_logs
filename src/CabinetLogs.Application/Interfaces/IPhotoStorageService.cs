using CabinetLogs.Application.DTOs.Logs;

namespace CabinetLogs.Application.Interfaces;

public interface IPhotoStorageService
{
    /// <summary>
    /// Uploads an image to Supabase Storage at logs/{logId}/{generatedFileName} and returns the stored object path.
    /// </summary>
    Task<string> UploadAsync(
        Guid logId,
        Stream content,
        string contentType,
        string extension,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an object from Supabase Storage. Returns true if deleted (or already absent), false on failure.
    /// </summary>
    Task<bool> TryDeleteAsync(string objectPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a short-lived signed URL for a private photo object path.
    /// </summary>
    Task<PhotoUrlResponse> CreateSignedUrlAsync(string objectPath, CancellationToken cancellationToken = default);
}
