using System.ComponentModel.DataAnnotations;
using CabinetLogs.Domain.Enums;

namespace CabinetLogs.Application.DTOs.ClientDevices;

public sealed record CreateClientDeviceRequest
{
    [Required]
    [MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    public DevicePlatform? Platform { get; init; }

    [MaxLength(200)]
    public string? InstallationKey { get; init; }
}

public sealed record UpdateClientDeviceRequest
{
    [Required]
    [MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    public DevicePlatform? Platform { get; init; }
}

public sealed record ClientDeviceQuery
{
    public string? Search { get; init; }
    public string? InstallationKey { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record ClientDeviceDto(
    Guid Id,
    string Name,
    string? Platform,
    string? InstallationKey,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ClientDeviceReferenceDto(
    Guid Id,
    string Name,
    string? Platform);
