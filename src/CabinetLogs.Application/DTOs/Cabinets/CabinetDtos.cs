namespace CabinetLogs.Application.DTOs.Cabinets;

public sealed record CabinetQuery
{
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record CabinetReferenceDto(
    Guid Id,
    string Name);

public sealed record CabinetSummaryDto(
    Guid Id,
    string Name,
    int LogCount,
    DateTimeOffset? LastOccurredAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CabinetDetailDto(
    Guid Id,
    string Name,
    int LogCount,
    DateTimeOffset? LastOccurredAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
