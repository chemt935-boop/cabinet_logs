namespace CabinetLogs.Application.Common.Options;

public sealed class PhotoUploadOptions
{
    public const string SectionName = "PhotoUpload";

    /// <summary>
    /// Maximum allowed image file size in bytes per photo (default: 5 MB).
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>
    /// Maximum number of photos allowed on a single maintenance log (default: 10).
    /// </summary>
    public int MaxPhotosPerLog { get; set; } = 10;

    /// <summary>
    /// Allowed image MIME types.
    /// </summary>
    public string[] AllowedContentTypes { get; set; } =
    [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];
}
