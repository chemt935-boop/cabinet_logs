using System.ComponentModel.DataAnnotations;

namespace CabinetLogs.Api.Models;

/// <summary>
/// Multipart form payload for creating a log with optional photo uploads (multiple supported).
/// </summary>
public sealed class CreateLogFormRequest
{
    [Required]
    [MaxLength(150)]
    public string CabinetName { get; set; } = string.Empty;

    [Required]
    [MaxLength(10000)]
    public string Issue { get; set; } = string.Empty;

    /// <summary>
    /// Optional ISO 8601 date-time with offset (e.g. 2026-11-23T10:30:00+02:00). Defaults to current UTC time.
    /// </summary>
    public DateTimeOffset? OccurredAt { get; set; }

    /// <summary>
    /// Optional registered client device UUID.
    /// </summary>
    public Guid? ClientDeviceId { get; set; }

    /// <summary>
    /// Optional fallback device name when ClientDeviceId is omitted.
    /// </summary>
    [MaxLength(150)]
    public string? CreatedByDeviceName { get; set; }

    /// <summary>
    /// Optional image files (JPEG, PNG, or WebP). Multiple files supported.
    /// </summary>
    public List<IFormFile>? Photos { get; set; }
}

/// <summary>
/// Multipart form payload for updating a log, optionally adding new photos or removing existing ones.
/// </summary>
public sealed class UpdateLogFormRequest
{
    [Required]
    [MaxLength(150)]
    public string CabinetName { get; set; } = string.Empty;

    [Required]
    [MaxLength(10000)]
    public string Issue { get; set; } = string.Empty;

    /// <summary>
    /// Optional updated ISO 8601 date-time with offset. Leave omitted to keep existing value.
    /// </summary>
    public DateTimeOffset? OccurredAt { get; set; }

    /// <summary>
    /// Set to true to remove ALL existing photos.
    /// </summary>
    public bool RemovePhoto { get; set; }

    /// <summary>
    /// Optional IDs of specific existing photos to remove.
    /// </summary>
    public List<Guid>? RemovePhotoIds { get; set; }

    /// <summary>
    /// Optional additional image files (JPEG, PNG, or WebP) to attach.
    /// </summary>
    public List<IFormFile>? Photos { get; set; }
}
