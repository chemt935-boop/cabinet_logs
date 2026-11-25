using System.ComponentModel.DataAnnotations;
using CabinetLogs.Application.DTOs.Cabinets;
using CabinetLogs.Application.DTOs.ClientDevices;

namespace CabinetLogs.Application.DTOs.Logs;

public sealed record CreateLogRequest
{
    [Required]
    [MaxLength(150)]
    public string CabinetName { get; init; } = string.Empty;

    [Required]
    [MaxLength(10000)]
    public string Issue { get; init; } = string.Empty;

    /// <summary>
    /// Optional ISO 8601 date/time with timezone offset (e.g. 2026-11-23T10:30:00+02:00).
    /// Defaults to current UTC time when omitted.
    /// </summary>
    public DateTimeOffset? OccurredAt { get; init; }

    /// <summary>
    /// Optional registered client device UUID. When supplied, its name is snapshotted into CreatedByDeviceName.
    /// </summary>
    public Guid? ClientDeviceId { get; init; }

    /// <summary>
    /// Fallback device name snapshot when ClientDeviceId is omitted.
    /// </summary>
    [MaxLength(150)]
    public string? CreatedByDeviceName { get; init; }
}

public sealed record UpdateLogRequest
{
    [Required]
    [MaxLength(150)]
    public string CabinetName { get; init; } = string.Empty;

    [Required]
    [MaxLength(10000)]
    public string Issue { get; init; } = string.Empty;

    public DateTimeOffset? OccurredAt { get; init; }

    /// <summary>
    /// Set to true to remove ALL existing photos from the log.
    /// </summary>
    public bool RemovePhoto { get; init; }

    /// <summary>
    /// Optional list of specific existing photo IDs to remove while keeping others.
    /// </summary>
    public List<Guid>? RemovePhotoIds { get; init; }
}

public sealed record PhotoUploadFile(
    Stream Content,
    long Length,
    string ContentType,
    string OriginalFileName);

public sealed record LogQuery
{
    public Guid? CabinetId { get; init; }
    public string? CabinetName { get; init; }

    /// <summary>
    /// Inclusive lower bound (occurredAt >= from).
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// Exclusive upper bound (occurredAt &lt; to).
    /// </summary>
    public DateTimeOffset? To { get; init; }

    public string? Search { get; init; }

    /// <summary>
    /// Sort order: occurredAt_desc (default), occurredAt_asc, createdAt_desc, createdAt_asc.
    /// </summary>
    public string? Sort { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record LogPhotoRefDto(
    Guid Id,
    int SortOrder,
    DateTimeOffset CreatedAt);

public sealed record LogDto(
    Guid Id,
    CabinetReferenceDto Cabinet,
    string Issue,
    DateTimeOffset OccurredAt,
    bool PhotoAvailable,
    int PhotoCount,
    IReadOnlyList<LogPhotoRefDto> Photos,
    string CreatedByDeviceName,
    ClientDeviceReferenceDto? ClientDevice,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PhotoUrlResponse(
    string Url,
    DateTimeOffset ExpiresAt,
    int ExpiresInSeconds);

public sealed record LogPhotoUrlDto(
    Guid PhotoId,
    string Url,
    DateTimeOffset ExpiresAt,
    int ExpiresInSeconds);
