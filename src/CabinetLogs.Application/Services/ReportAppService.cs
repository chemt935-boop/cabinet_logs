using CabinetLogs.Application.Common.Exceptions;
using CabinetLogs.Application.DTOs.Reports;
using CabinetLogs.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CabinetLogs.Application.Services;

public sealed class ReportAppService : IReportAppService
{
    private readonly IApplicationDbContext _db;
    private readonly IExcelReportService _excelReportService;

    public ReportAppService(
        IApplicationDbContext db,
        IExcelReportService excelReportService)
    {
        _db = db;
        _excelReportService = excelReportService;
    }

    public async Task<GeneratedFileResult> GenerateCabinetExcelReportAsync(
        Guid cabinetId,
        CabinetExcelReportQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.From.HasValue && query.To.HasValue && query.From.Value >= query.To.Value)
        {
            throw new BadRequestException("'from' must be earlier than 'to'.", new Dictionary<string, string[]>
            {
                ["from"] = ["'from' must be earlier than 'to'."]
            });
        }

        var cabinet = await _db.Cabinets
            .AsNoTracking()
            .Where(c => c.Id == cabinetId)
            .Select(c => new { c.Id, c.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (cabinet is null)
        {
            throw new NotFoundException($"Cabinet '{cabinetId}' was not found.");
        }

        var logsQuery = _db.Logs
            .AsNoTracking()
            .Where(l => l.CabinetId == cabinetId);

        DateTimeOffset? fromUtc = null;
        if (query.From.HasValue)
        {
            fromUtc = query.From.Value.ToUniversalTime();
            logsQuery = logsQuery.Where(l => l.OccurredAt >= fromUtc.Value);
        }

        DateTimeOffset? toUtc = null;
        if (query.To.HasValue)
        {
            toUtc = query.To.Value.ToUniversalTime();
            logsQuery = logsQuery.Where(l => l.OccurredAt < toUtc.Value);
        }

        var sortKey = string.IsNullOrWhiteSpace(query.Sort) ? "occurredAt_asc" : query.Sort.Trim();
        logsQuery = sortKey switch
        {
            "occurredAt_asc" => logsQuery.OrderBy(l => l.OccurredAt).ThenBy(l => l.CreatedAt),
            "occurredAt_desc" => logsQuery.OrderByDescending(l => l.OccurredAt).ThenByDescending(l => l.CreatedAt),
            _ => throw new BadRequestException(
                $"Invalid sort value '{query.Sort}'. Allowed values: occurredAt_asc, occurredAt_desc.")
        };

        // Intentionally project ONLY text/date fields — NO photo paths or URLs per specification section 3.5.
        var logs = await logsQuery
            .Select(l => new ExcelReportLogItem(
                l.Id,
                l.OccurredAt,
                l.Issue,
                l.CreatedByDeviceName,
                l.CreatedAt))
            .ToListAsync(cancellationToken);

        var reportData = new ExcelReportData(
            cabinet.Id,
            cabinet.Name,
            fromUtc,
            toUtc,
            DateTimeOffset.UtcNow,
            logs);

        return await _excelReportService.GenerateCabinetReportAsync(reportData, cancellationToken);
    }
}
