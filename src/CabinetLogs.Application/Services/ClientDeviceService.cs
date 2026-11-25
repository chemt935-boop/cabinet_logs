using CabinetLogs.Application.Common;
using CabinetLogs.Application.Common.Exceptions;
using CabinetLogs.Application.DTOs.ClientDevices;
using CabinetLogs.Application.Interfaces;
using CabinetLogs.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CabinetLogs.Application.Services;

public sealed class ClientDeviceService : IClientDeviceService
{
    private const int MaxNameLength = 150;
    private const int MaxInstallationKeyLength = 200;
    private const int MaxPageSize = 100;

    private readonly IApplicationDbContext _db;
    private readonly IDatabaseErrorInspector _errorInspector;

    public ClientDeviceService(
        IApplicationDbContext db,
        IDatabaseErrorInspector errorInspector)
    {
        _db = db;
        _errorInspector = errorInspector;
    }

    public async Task<ClientDeviceDto> CreateAsync(
        CreateClientDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = ValidateName(request.Name);
        var installationKey = NormalizeOptionalKey(request.InstallationKey);

        if (installationKey is not null)
        {
            var exists = await _db.ClientDevices
                .AsNoTracking()
                .AnyAsync(d => d.InstallationKey == installationKey, cancellationToken);

            if (exists)
            {
                throw new ConflictException(
                    $"A client device with installationKey '{installationKey}' is already registered.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new ClientDevice
        {
            Id = Guid.NewGuid(),
            Name = name,
            Platform = request.Platform,
            InstallationKey = installationKey,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.ClientDevices.Add(entity);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (_errorInspector.IsUniqueConstraintViolation(ex))
        {
            throw new ConflictException(
                $"A client device with installationKey '{installationKey}' is already registered.");
        }

        return MapToDto(entity);
    }

    public async Task<PagedResult<ClientDeviceDto>> GetAllAsync(
        ClientDeviceQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidatePagination(query.Page, query.PageSize);

        var q = _db.ClientDevices.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.InstallationKey))
        {
            var key = query.InstallationKey.Trim();
            q = q.Where(d => d.InstallationKey == key);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(d => d.Name.ToLower().Contains(term));
        }

        var totalCount = await q.CountAsync(cancellationToken);

        var entities = await q
            .OrderBy(d => d.Name)
            .ThenBy(d => d.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = entities.Select(MapToDto).ToList();
        return PagedResult<ClientDeviceDto>.Create(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<ClientDeviceDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.ClientDevices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (entity is null)
        {
            throw new NotFoundException($"Client device '{id}' was not found.");
        }

        return MapToDto(entity);
    }

    public async Task<ClientDeviceDto> UpdateAsync(
        Guid id,
        UpdateClientDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = ValidateName(request.Name);

        var entity = await _db.ClientDevices
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (entity is null)
        {
            throw new NotFoundException($"Client device '{id}' was not found.");
        }

        // Historical logs preserve their CreatedByDeviceName snapshot per specification.
        entity.Name = name;
        entity.Platform = request.Platform;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return MapToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.ClientDevices
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (entity is null)
        {
            throw new NotFoundException($"Client device '{id}' was not found.");
        }

        // Foreign key is configured with ON DELETE SET NULL so historical logs and their
        // CreatedByDeviceName snapshots are preserved.
        _db.ClientDevices.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string ValidateName(string? rawName)
    {
        var name = rawName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BadRequestException("Device name is required.", new Dictionary<string, string[]>
            {
                ["name"] = ["Device name is required."]
            });
        }

        if (name.Length > MaxNameLength)
        {
            throw new BadRequestException($"Device name cannot exceed {MaxNameLength} characters.", new Dictionary<string, string[]>
            {
                ["name"] = [$"Device name cannot exceed {MaxNameLength} characters."]
            });
        }

        return name;
    }

    private static string? NormalizeOptionalKey(string? rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return null;
        }

        var key = rawKey.Trim();
        if (key.Length > MaxInstallationKeyLength)
        {
            throw new BadRequestException(
                $"Installation key cannot exceed {MaxInstallationKeyLength} characters.",
                new Dictionary<string, string[]>
                {
                    ["installationKey"] = [$"Installation key cannot exceed {MaxInstallationKeyLength} characters."]
                });
        }

        return key;
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

    private static ClientDeviceDto MapToDto(ClientDevice entity) => new(
        entity.Id,
        entity.Name,
        entity.Platform?.ToString(),
        entity.InstallationKey,
        entity.CreatedAt,
        entity.UpdatedAt);
}
