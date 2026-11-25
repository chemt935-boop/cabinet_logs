using CabinetLogs.Application.Common.Exceptions;
using CabinetLogs.Application.Common.Options;

namespace CabinetLogs.Application.Common;

public static class PhotoValidator
{
    public static string ValidateAndGetExtension(
        Stream stream,
        long length,
        string? contentType,
        PhotoUploadOptions options)
    {
        if (length <= 0)
        {
            throw new BadRequestException("The uploaded photo file is empty.");
        }

        if (length > options.MaxFileSizeBytes)
        {
            var maxMb = Math.Round(options.MaxFileSizeBytes / (1024.0 * 1024.0), 2);
            throw new PayloadTooLargeException(
                $"Photo exceeds the maximum allowed size of {maxMb} MB ({options.MaxFileSizeBytes} bytes).");
        }

        var normalizedContentType = (contentType ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedContentType == "image/jpg")
        {
            normalizedContentType = "image/jpeg";
        }

        var allowed = options.AllowedContentTypes
            .Select(t => t.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!allowed.Contains(normalizedContentType))
        {
            throw new UnsupportedMediaTypeException(
                $"Unsupported photo content type '{contentType}'. Allowed types: {string.Join(", ", allowed)}.");
        }

        // Verify file signature (magic bytes) to prevent spoofed content types
        if (!stream.CanSeek)
        {
            return MapExtension(normalizedContentType);
        }

        var originalPosition = stream.Position;
        try
        {
            Span<byte> header = stackalloc byte[12];
            var bytesRead = stream.Read(header);

            var validSignature = normalizedContentType switch
            {
                "image/jpeg" => bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
                "image/png" => bytesRead >= 8
                    && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                    && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A,
                "image/webp" => bytesRead >= 12
                    && header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F'
                    && header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P',
                _ => true
            };

            if (!validSignature)
            {
                throw new UnsupportedMediaTypeException(
                    $"The uploaded file content does not match the declared media type '{normalizedContentType}'.");
            }
        }
        finally
        {
            stream.Position = originalPosition;
        }

        return MapExtension(normalizedContentType);
    }

    private static string MapExtension(string normalizedContentType) => normalizedContentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".bin"
    };
}
