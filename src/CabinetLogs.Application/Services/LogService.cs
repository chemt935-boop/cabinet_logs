using CabinetLogs.Application.Common;
using CabinetLogs.Application.Common.Exceptions;
using CabinetLogs.Application.Common.Options;
using CabinetLogs.Application.DTOs.Cabinets;
using CabinetLogs.Application.DTOs.ClientDevices;
using CabinetLogs.Application.DTOs.Logs;
using CabinetLogs.Application.Interfaces;
using CabinetLogs.Domain.Common;
using CabinetLogs.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CabinetLogs.Application.Services;

public sealed class LogService : ILogService
{
    private const int MaxCabinetNameLength = 150;
    private const int MaxIssueLength = 10_000;
    private const int MaxDeviceNameLength = 150;
    private const int MaxPageSize = 100;

    private readonly IApplicationDbContext _db;
    private readonly IDatabaseErrorInspector _errorInspector;
    private readonly IPhotoStorageService _photoStorage;
    private readonly PhotoUploadOptions _photoOptions;
    private readonly ILogger<LogService> _logger;

    public LogService(
        IApplicationDbContext db,
        IDatabaseErrorInspector errorInspector,
        IPhotoStorageService photoStorage,
        IOptions<PhotoUploadOptions> photoOptions,
        ILogger<LogService> logger)
    {
        _db = db;
        _errorInspector = errorInspector;
        _photoStorage = photoStorage;
        _photoOptions = photoOptions.Value;
        _logger = logger;
    }

    public async Task<LogDto> CreateAsync(
        CreateLogRequest request,
        IReadOnlyList<PhotoUploadFile>? photos,
        CancellationToken cancellationToken = default)
    {
        var displayCabinetName = ValidateCabinetName(request.CabinetName);
        var normalizedCabinetName = CabinetNameNormalizer.Normalize(displayCabinetName);
        var issue = ValidateIssue(request.Issue);

        var (clientDevice, createdByDeviceName) = await ResolveDeviceAsync(
            request.ClientDeviceId,
            request.CreatedByDeviceName,
            cancellationToken);

        var photoList = photos ?? [];
        var maxPhotos = _photoOptions.MaxPhotosPerLog > 0 ? _photoOptions.MaxPhotosPerLog : 10;
        if (photoList.Count > maxPhotos)
        {
            throw new BadRequestException(
                $"A maximum of {maxPhotos} photos can be attached to a single log.",
                new Dictionary<string, string[]>
                {
                    ["photos"] = [$"You can upload at most {maxPhotos} photos per log."]
                });
        }

        // Validate ALL photos before uploading any of them.
        var validatedPhotos = new List<(PhotoUploadFile File, string Extension)>(photoList.Count);
        foreach (var photo in photoList)
        {
            var ext = PhotoValidator.ValidateAndGetExtension(
                photo.Content,
                photo.Length,
                photo.ContentType,
                _photoOptions);
            validatedPhotos.Add((photo, ext));
        }

        var logId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var occurredAt = (request.OccurredAt ?? now).ToUniversalTime();

        var uploadedPaths = new List<string>(validatedPhotos.Count);
        try
        {
            foreach (var (file, ext) in validatedPhotos)
            {
                var path = await _photoStorage.UploadAsync(
                    logId,
                    file.Content,
                    file.ContentType,
                    ext,
                    cancellationToken);
                uploadedPaths.Add(path);
            }

            return await _db.ExecuteInTransactionAsync(async (transaction, ct) =>
            {
                var cabinet = await FindOrCreateCabinetAsync(
                    displayCabinetName,
                    normalizedCabinetName,
                    now,
                    transaction,
                    ct);

                var log = new MaintenanceLog
                {
                    Id = logId,
                    CabinetId = cabinet.Id,
                    Issue = issue,
                    OccurredAt = occurredAt,
                    PhotoPath = uploadedPaths.FirstOrDefault(),
                    ClientDeviceId = clientDevice?.Id,
                    CreatedByDeviceName = createdByDeviceName,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                var logPhotos = uploadedPaths.Select((path, index) => new LogPhoto
                {
                    Id = Guid.NewGuid(),
                    LogId = logId,
                    PhotoPath = path,
                    SortOrder = index,
                    CreatedAt = now
                }).ToList();

                log.Photos = logPhotos;

                _db.Logs.Add(log);
                await _db.SaveChangesAsync(ct);

                return MapToDto(log, cabinet, clientDevice);
            }, cancellationToken);
        }
        catch
        {
            foreach (var path in uploadedPaths)
            {
                await CleanupOrQueueStorageObjectAsync(path, CancellationToken.None);
            }

            throw;
        }
    }

    public async Task<PagedResult<LogDto>> GetAllAsync(
        LogQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateLogQuery(query);

        var q = _db.Logs
            .AsNoTracking()
            .Include(l => l.Cabinet)
            .Include(l => l.ClientDevice)
            .Include(l => l.Photos)
            .AsQueryable();

        if (query.CabinetId.HasValue)
        {
            q = q.Where(l => l.CabinetId == query.CabinetId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.CabinetName))
        {
            var normalizedSearch = CabinetNameNormalizer.Normalize(query.CabinetName);
            q = q.Where(l => l.Cabinet.NormalizedName.Contains(normalizedSearch));
        }

        if (query.From.HasValue)
        {
            var fromUtc = query.From.Value.ToUniversalTime();
            q = q.Where(l => l.OccurredAt >= fromUtc);
        }

        if (query.To.HasValue)
        {
            // Exclusive upper bound (occurredAt < to) per specification section 7.3
            var toUtc = query.To.Value.ToUniversalTime();
            q = q.Where(l => l.OccurredAt < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(l => l.Issue.ToLower().Contains(term));
        }

        var totalCount = await q.CountAsync(cancellationToken);

        var sortKey = string.IsNullOrWhiteSpace(query.Sort)
            ? "occurredAt_desc"
            : query.Sort.Trim();

        q = sortKey switch
        {
            "occurredAt_desc" => q.OrderByDescending(l => l.OccurredAt).ThenByDescending(l => l.CreatedAt),
            "occurredAt_asc" => q.OrderBy(l => l.OccurredAt).ThenBy(l => l.CreatedAt),
            "createdAt_desc" => q.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id),
            "createdAt_asc" => q.OrderBy(l => l.CreatedAt).ThenBy(l => l.Id),
            _ => throw new BadRequestException(
                $"Invalid sort value '{query.Sort}'. Allowed values: occurredAt_desc, occurredAt_asc, createdAt_desc, createdAt_asc.")
        };

        var entities = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = entities.Select(e => MapToDto(e, e.Cabinet, e.ClientDevice)).ToList();
        return PagedResult<LogDto>.Create(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<LogDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Logs
            .AsNoTracking()
            .Include(l => l.Cabinet)
            .Include(l => l.ClientDevice)
            .Include(l => l.Photos)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        if (entity is null)
        {
            throw new NotFoundException($"Log '{id}' was not found.");
        }

        return MapToDto(entity, entity.Cabinet, entity.ClientDevice);
    }

    public async Task<LogDto> UpdateAsync(
        Guid id,
        UpdateLogRequest request,
        IReadOnlyList<PhotoUploadFile>? newPhotos,
        CancellationToken cancellationToken = default)
    {
        var displayCabinetName = ValidateCabinetName(request.CabinetName);
        var normalizedCabinetName = CabinetNameNormalizer.Normalize(displayCabinetName);
        var issue = ValidateIssue(request.Issue);

        var incomingPhotos = newPhotos ?? [];
        var validatedPhotos = new List<(PhotoUploadFile File, string Extension)>(incomingPhotos.Count);
        foreach (var photo in incomingPhotos)
        {
            var ext = PhotoValidator.ValidateAndGetExtension(
                photo.Content,
                photo.Length,
                photo.ContentType,
                _photoOptions);
            validatedPhotos.Add((photo, ext));
        }

        var entity = await _db.Logs
            .Include(l => l.Cabinet)
            .Include(l => l.ClientDevice)
            .Include(l => l.Photos)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        if (entity is null)
        {
            throw new NotFoundException($"Log '{id}' was not found.");
        }

        var removeIds = request.RemovePhotoIds?.ToHashSet() ?? [];
        var existingPhotosToRemove = request.RemovePhoto
            ? entity.Photos.ToList()
            : entity.Photos.Where(p => removeIds.Contains(p.Id)).ToList();

        var remainingCount = entity.Photos.Count - existingPhotosToRemove.Count;
        var maxPhotos = _photoOptions.MaxPhotosPerLog > 0 ? _photoOptions.MaxPhotosPerLog : 10;
        if (remainingCount + validatedPhotos.Count > maxPhotos)
        {
            throw new BadRequestException(
                $"A maximum of {maxPhotos} photos can be attached to a single log.",
                new Dictionary<string, string[]>
                {
                    ["photos"] = [$"Total photos ({remainingCount + validatedPhotos.Count}) exceeds the limit of {maxPhotos}."]
                });
        }

        // Upload new files BEFORE mutating the database.
        var uploadedNewPaths = new List<string>(validatedPhotos.Count);
        var pathsToDeleteAfterCommit = new List<string>();
        var now = DateTimeOffset.UtcNow;
        Cabinet targetCabinet;

        try
        {
            foreach (var (file, ext) in validatedPhotos)
            {
                var path = await _photoStorage.UploadAsync(
                    entity.Id,
                    file.Content,
                    file.ContentType,
                    ext,
                    cancellationToken);
                uploadedNewPaths.Add(path);
            }

            targetCabinet = await _db.ExecuteInTransactionAsync(async (transaction, ct) =>
            {
                Cabinet resolvedCabinet;
                if (string.Equals(entity.Cabinet.NormalizedName, normalizedCabinetName, StringComparison.Ordinal))
                {
                    resolvedCabinet = entity.Cabinet;
                }
                else
                {
                    resolvedCabinet = await FindOrCreateCabinetAsync(
                        displayCabinetName,
                        normalizedCabinetName,
                        now,
                        transaction,
                        ct);
                    entity.CabinetId = resolvedCabinet.Id;
                    entity.Cabinet = resolvedCabinet;
                }

                entity.Issue = issue;
                if (request.OccurredAt.HasValue)
                {
                    entity.OccurredAt = request.OccurredAt.Value.ToUniversalTime();
                }

                // Handle legacy single photo_path if log had one without a log_photos row
                if (request.RemovePhoto
                    && !string.IsNullOrWhiteSpace(entity.PhotoPath)
                    && entity.Photos.All(p => !string.Equals(p.PhotoPath, entity.PhotoPath, StringComparison.Ordinal)))
                {
                    pathsToDeleteAfterCommit.Add(entity.PhotoPath);
                }

                foreach (var oldPhoto in existingPhotosToRemove)
                {
                    pathsToDeleteAfterCommit.Add(oldPhoto.PhotoPath);
                    entity.Photos.Remove(oldPhoto);
                    _db.LogPhotos.Remove(oldPhoto);
                }

                var nextSortOrder = entity.Photos.Count > 0
                    ? entity.Photos.Max(p => p.SortOrder) + 1
                    : 0;

                foreach (var newPath in uploadedNewPaths)
                {
                    var photoRow = new LogPhoto
                    {
                        Id = Guid.NewGuid(),
                        LogId = entity.Id,
                        PhotoPath = newPath,
                        SortOrder = nextSortOrder++,
                        CreatedAt = now
                    };
                    entity.Photos.Add(photoRow);
                    _db.LogPhotos.Add(photoRow);
                }

                entity.PhotoPath = entity.Photos
                    .OrderBy(p => p.SortOrder)
                    .Select(p => p.PhotoPath)
                    .FirstOrDefault();

                entity.UpdatedAt = now;

                await _db.SaveChangesAsync(ct);
                return resolvedCabinet;
            }, cancellationToken);
        }
        catch
        {
            foreach (var newPath in uploadedNewPaths)
            {
                await CleanupOrQueueStorageObjectAsync(newPath, CancellationToken.None);
            }

            throw;
        }

        // Delete removed old photos after DB update succeeded
        foreach (var oldPath in pathsToDeleteAfterCommit.Distinct(StringComparer.Ordinal))
        {
            await CleanupOrQueueStorageObjectAsync(oldPath, cancellationToken);
        }

        return MapToDto(entity, targetCabinet, entity.ClientDevice);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Logs
            .Include(l => l.Photos)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        if (entity is null)
        {
            throw new NotFoundException($"Log '{id}' was not found.");
        }

        var photoPaths = entity.Photos
            .Select(p => p.PhotoPath)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (!string.IsNullOrWhiteSpace(entity.PhotoPath)
            && !photoPaths.Contains(entity.PhotoPath, StringComparer.Ordinal))
        {
            photoPaths.Add(entity.PhotoPath);
        }

        // Per specification section 3.2: do NOT automatically delete the cabinet when its last log is deleted.
        _db.Logs.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var photoPath in photoPaths)
        {
            await CleanupOrQueueStorageObjectAsync(photoPath, cancellationToken);
        }
    }

    public async Task<PhotoUrlResponse> GetPhotoUrlAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var photos = await GetPhotoUrlsAsync(id, cancellationToken);
        if (photos.Count == 0)
        {
            throw new NotFoundException($"Log '{id}' does not have any attached photos.");
        }

        var first = photos[0];
        return new PhotoUrlResponse(first.Url, first.ExpiresAt, first.ExpiresInSeconds);
    }

    public async Task<IReadOnlyList<LogPhotoUrlDto>> GetPhotoUrlsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var log = await _db.Logs
            .AsNoTracking()
            .Include(l => l.Photos)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        if (log is null)
        {
            throw new NotFoundException($"Log '{id}' was not found.");
        }

        var orderedPhotos = log.Photos
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.CreatedAt)
            .ToList();

        var results = new List<LogPhotoUrlDto>();

        if (orderedPhotos.Count > 0)
        {
            foreach (var photo in orderedPhotos)
            {
                var signed = await _photoStorage.CreateSignedUrlAsync(photo.PhotoPath, cancellationToken);
                results.Add(new LogPhotoUrlDto(photo.Id, signed.Url, signed.ExpiresAt, signed.ExpiresInSeconds));
            }
        }
        else if (!string.IsNullOrWhiteSpace(log.PhotoPath))
        {
            var signed = await _photoStorage.CreateSignedUrlAsync(log.PhotoPath, cancellationToken);
            results.Add(new LogPhotoUrlDto(log.Id, signed.Url, signed.ExpiresAt, signed.ExpiresInSeconds));
        }

        return results;
    }

    private async Task<Cabinet> FindOrCreateCabinetAsync(
        string displayName,
        string normalizedName,
        DateTimeOffset now,
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var existing = await _db.Cabinets
            .FirstOrDefaultAsync(c => c.NormalizedName == normalizedName, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        const string savepointName = "before_cabinet_insert";
        var usedSavepoint = false;

        if (transaction is not null && transaction.SupportsSavepoints)
        {
            await transaction.CreateSavepointAsync(savepointName, cancellationToken);
            usedSavepoint = true;
        }

        var newCabinet = new Cabinet
        {
            Id = Guid.NewGuid(),
            Name = displayName,
            NormalizedName = normalizedName,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Cabinets.Add(newCabinet);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);

            if (usedSavepoint && transaction is not null)
            {
                await transaction.ReleaseSavepointAsync(savepointName, cancellationToken);
            }

            return newCabinet;
        }
        catch (DbUpdateException ex) when (_errorInspector.IsUniqueConstraintViolation(ex))
        {
            _logger.LogInformation(
                "Concurrent creation detected for cabinet '{NormalizedName}'. Re-fetching existing record.",
                normalizedName);

            if (usedSavepoint && transaction is not null)
            {
                await transaction.RollbackToSavepointAsync(savepointName, cancellationToken);
            }

            _db.Detach(newCabinet);

            var racedCabinet = await _db.Cabinets
                .FirstOrDefaultAsync(c => c.NormalizedName == normalizedName, cancellationToken);

            if (racedCabinet is null)
            {
                throw;
            }

            return racedCabinet;
        }
    }

    private async Task<(ClientDevice? Device, string SnapshotName)> ResolveDeviceAsync(
        Guid? clientDeviceId,
        string? fallbackDeviceName,
        CancellationToken cancellationToken)
    {
        if (clientDeviceId.HasValue)
        {
            var device = await _db.ClientDevices
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == clientDeviceId.Value, cancellationToken);

            if (device is null)
            {
                throw new BadRequestException(
                    $"Client device '{clientDeviceId.Value}' does not exist.",
                    new Dictionary<string, string[]>
                    {
                        ["clientDeviceId"] = [$"Client device '{clientDeviceId.Value}' was not found."]
                    });
            }

            return (device, device.Name);
        }

        var trimmedFallback = fallbackDeviceName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedFallback))
        {
            throw new BadRequestException(
                "Either clientDeviceId or createdByDeviceName must be provided.",
                new Dictionary<string, string[]>
                {
                    ["clientDeviceId"] = ["Provide a valid registered clientDeviceId or a non-empty createdByDeviceName."],
                    ["createdByDeviceName"] = ["Provide a non-empty createdByDeviceName when clientDeviceId is omitted."]
                });
        }

        if (trimmedFallback.Length > MaxDeviceNameLength)
        {
            throw new BadRequestException(
                $"createdByDeviceName cannot exceed {MaxDeviceNameLength} characters.",
                new Dictionary<string, string[]>
                {
                    ["createdByDeviceName"] = [$"createdByDeviceName cannot exceed {MaxDeviceNameLength} characters."]
                });
        }

        return (null, trimmedFallback);
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
                failureReason = "Storage provider returned non-success status during delete.";
            }
        }
        catch (Exception ex)
        {
            failureReason = ex.Message;
            _logger.LogWarning(ex, "Failed to delete storage object '{ObjectPath}'. Queuing for retry.", objectPath);
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
            _logger.LogError(
                ex,
                "Failed to record pending storage cleanup for orphaned object '{ObjectPath}'.",
                objectPath);
        }
    }

    private static string ValidateCabinetName(string? rawName)
    {
        var cleaned = CabinetNameNormalizer.CleanDisplayName(rawName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            throw new BadRequestException("Cabinet name is required.", new Dictionary<string, string[]>
            {
                ["cabinetName"] = ["Cabinet name is required."]
            });
        }

        if (cleaned.Length > MaxCabinetNameLength)
        {
            throw new BadRequestException(
                $"Cabinet name cannot exceed {MaxCabinetNameLength} characters.",
                new Dictionary<string, string[]>
                {
                    ["cabinetName"] = [$"Cabinet name cannot exceed {MaxCabinetNameLength} characters."]
                });
        }

        return cleaned;
    }

    private static string ValidateIssue(string? rawIssue)
    {
        var issue = rawIssue?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(issue))
        {
            throw new BadRequestException("Issue description is required.", new Dictionary<string, string[]>
            {
                ["issue"] = ["Issue description is required."]
            });
        }

        if (issue.Length > MaxIssueLength)
        {
            throw new BadRequestException(
                $"Issue description cannot exceed {MaxIssueLength} characters.",
                new Dictionary<string, string[]>
                {
                    ["issue"] = [$"Issue description cannot exceed {MaxIssueLength} characters."]
                });
        }

        return issue;
    }

    private static void ValidateLogQuery(LogQuery query)
    {
        var errors = new Dictionary<string, string[]>();

        if (query.Page < 1)
        {
            errors["page"] = ["Page must be greater than or equal to 1."];
        }

        if (query.PageSize < 1 || query.PageSize > MaxPageSize)
        {
            errors["pageSize"] = [$"PageSize must be between 1 and {MaxPageSize}."];
        }

        if (query.From.HasValue && query.To.HasValue && query.From.Value >= query.To.Value)
        {
            errors["from"] = ["'from' must be earlier than 'to'."];
        }

        if (errors.Count > 0)
        {
            throw new BadRequestException("Invalid query parameters.", errors);
        }
    }

    private static LogDto MapToDto(MaintenanceLog log, Cabinet cabinet, ClientDevice? clientDevice)
    {
        var photoRefs = log.Photos
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.CreatedAt)
            .Select(p => new LogPhotoRefDto(p.Id, p.SortOrder, p.CreatedAt))
            .ToList();

        var hasAnyPhoto = photoRefs.Count > 0 || !string.IsNullOrWhiteSpace(log.PhotoPath);
        var count = photoRefs.Count > 0 ? photoRefs.Count : (hasAnyPhoto ? 1 : 0);

        return new LogDto(
            log.Id,
            new CabinetReferenceDto(cabinet.Id, cabinet.Name),
            log.Issue,
            log.OccurredAt,
            hasAnyPhoto,
            count,
            photoRefs,
            log.CreatedByDeviceName,
            clientDevice is null
                ? null
                : new ClientDeviceReferenceDto(clientDevice.Id, clientDevice.Name, clientDevice.Platform?.ToString()),
            log.CreatedAt,
            log.UpdatedAt);
    }
}
