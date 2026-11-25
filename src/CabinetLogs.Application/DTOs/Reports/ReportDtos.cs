namespace CabinetLogs.Application.DTOs.Reports;

public sealed record CabinetExcelReportQuery
{
    /// <summary>
    /// Optional inclusive start of the report date/time range (occurredAt >= from).
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// Optional exclusive end of the report date/time range (occurredAt &lt; to).
    /// </summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>
    /// Chronological sort direction: occurredAt_asc (default) or occurredAt_desc.
    /// </summary>
    public string? Sort { get; init; }
}

public sealed record ExcelReportLogItem(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Issue,
    string CreatedByDeviceName,
    DateTimeOffset CreatedAt);

public sealed record ExcelReportData(
    Guid CabinetId,
    string CabinetName,
    DateTimeOffset? From,
    DateTimeOffset? To,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<ExcelReportLogItem> Logs);

public sealed record GeneratedFileResult(
    byte[] Content,
    string ContentType,
    string FileName);
