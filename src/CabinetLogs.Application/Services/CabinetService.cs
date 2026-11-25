using CabinetLogs.Application.Common;
using CabinetLogs.Application.Common.Exceptions;
using CabinetLogs.Application.DTOs.Cabinets;
using CabinetLogs.Application.DTOs.Logs;
using CabinetLogs.Application.Interfaces;
using CabinetLogs.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CabinetLogs.Application.Services;

public sealed class CabinetService : ICabinetService
{
    private const int MaxPageSize = 100;

    private readonly IApplicationDbContext _db;
    private readonly ILogService _logService;
    private readonly IPhotoStorageService _photoStorage;
    private readonly ILogger<CabinetService> _logger;

    public CabinetService(
        IApplicationDbContext db,
        ILogService logService,
        IPhotoStorageService photoStorage,
        ILogger<CabinetService> logger)
    {
        _db = db;
        _logService = logService;
        _photoStorage = photoStorage;
        _logger = logger;
    }

    public async Task<PagedResult<CabinetSummaryDto>> GetAllAsync(
        CabinetQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidatePagination(query.Page, query.PageSize);

        var q = _db.Cabinets.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(c => c.NormalizedName.Contains(term) || c.Name.ToLower().Contains(term));
        }

        var totalCount = await q.CountAsync(cancellationToken);

        var items = await q
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(c => new CabinetSummaryDto(
                c.Id,
                c.Name,
                c.Logs.Count(),
                c.Logs.Max(l => (DateTimeOffset?)l.OccurredAt),
                c.CreatedAt,
                c.UpdatedAt))
            .ToListAsync(cancellationToken);

        return PagedResult<CabinetSummaryDto>.Create(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<CabinetDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cabinet = await _db.Cabinets
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CabinetDetailDto(
                c.Id,
                c.Name,
                c.Logs.Count(),
                c.Logs.Max(l => (DateTimeOffset?)l.OccurredAt),
                c.CreatedAt,
                c.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (cabinet is null)
        {
            throw new NotFoundException($"Cabinet '{id}' was not found.");
        }

        return cabinet;
    }

    public async Task<PagedResult<LogDto>> GetLogsByCabinetIdAsync(
        Guid cabinetId,
        LogQuery query,
        CancellationToken cancellationToken = default)
    {
        var exists = await _db.Cabinets
            .AsNoTracking()
            .AnyAsync(c => c.Id == cabinetId, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException($"Cabinet '{cabinetId}' was not found.");
        }

        var scopedQuery = query with { CabinetId = cabinetId, CabinetName = null };
        return await _logService.GetAllAsync(scopedQuery, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, bool force = false, CancellationToken cancellationToken = default)
    {
        var cabinet = await _db.Cabinets
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (cabinet is null)
        {
            throw new NotFoundException($"Cabinet '{id}' was not found.");
        }

        var logCount = await _db.Logs
            .AsNoTracking()
            .CountAsync(l => l.CabinetId == id, cancellationToken);

        if (logCount > 0 && !force)
        {
            throw new ConflictException(
                $"Cabinet '{cabinet.Name}' still has {logCount} log(s). Delete its logs first or pass force=true to delete the cabinet and all its logs.");
        }

        var photoPathsToCleanup = await _db.ExecuteInTransactionAsync(async (_, ct) =>
        {
            var paths = new List<string>();

            if (logCount > 0)
            {
                var logs = await _db.Logs
                    .Include(l => l.Photos)
                    .Where(l => l.CabinetId == id)
                    .ToListAsync(ct);

                foreach (var log in logs)
                {
                    foreach (var p in log.Photos)
                    {
                        if (!string.IsNullOrWhiteSpace(p.PhotoPath))
                        {
                            paths.Add(p.PhotoPath);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(log.PhotoPath))
                    {
                        paths.Add(log.PhotoPath);
                    }
                }

                _db.Logs.RemoveRange(logs);
                await _db.SaveChangesAsync(ct);
            }

            _db.Cabinets.Remove(cabinet);
            await _db.SaveChangesAsync(ct);
            return paths.Distinct(StringComparer.Ordinal).ToList();
        }, cancellationToken);

        foreach (var path in photoPathsToCleanup)
        {
            await CleanupOrQueueStorageObjectAsync(path, cancellationToken);
        }
    }

    private async Task CleanupOrQueueStorageObjectAsync(string objectPath, CancellationToken cancellationToken)
    {
        var deleted = false;
        string? failureReason = null;

        try
        {
            deleted = await _photoStorage.TryDeleteAsync(objectPath, cancellationToken);
            if (!deleted)
            {
                failureReason = "Storage provider returned non-success status during cabinet deletion cleanup.";
            }
        }
        catch (Exception ex)
        {
            failureReason = ex.Message;
            _logger.LogWarning(ex, "Failed to delete storage object '{ObjectPath}' while deleting cabinet.", objectPath);
        }

        if (deleted)
        {
            return;
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            _db.PendingStorageCleanups.Add(new PendingStorageCleanup
            {
                Id = Guid.NewGuid(),
                ObjectPath = objectPath,
                AttemptCount = 1,
                LastError = failureReason is { Length: > 1000 } ? failureReason[..1000] : failureReason,
                CreatedAt = now,
                LastAttemptedAt = now,
                NextRetryAt = now.AddMinutes(5)
            });

            await _db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue storage cleanup for '{ObjectPath}'.", objectPath);
        }
    }

    private static void ValidatePagination(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (page < 1)
        {
            errors["page"] = ["Page must be greater than or equal to 1."];
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            errors["pageSize"] = [$"PageSize must be between 1 and {MaxPageSize}."];
        }

        if (errors.Count > 0)
        {
            throw new BadRequestException("Invalid pagination parameters.", errors);
        }
    }
}
