using CabinetLogs.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CabinetLogs.Infrastructure.Storage;

/// <summary>
/// Periodically retries deleting orphaned Supabase Storage objects recorded in pending_storage_cleanups.
/// </summary>
public sealed class StorageCleanupBackgroundService : BackgroundService
{
    private const int MaxAttempts = 10;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StorageCleanupBackgroundService> _logger;

    public StorageCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<StorageCleanupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var ticked = await timer.WaitForNextTickAsync(stoppingToken);
                if (!ticked)
                {
                    break;
                }

                await ProcessPendingCleanupsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during scheduled storage cleanup retry run.");
            }
        }
    }

    private async Task ProcessPendingCleanupsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IPhotoStorageService>();

        var now = DateTimeOffset.UtcNow;
        var dueItems = await db.PendingStorageCleanups
            .Where(p => p.NextRetryAt <= now && p.AttemptCount < MaxAttempts)
            .OrderBy(p => p.NextRetryAt)
            .Take(25)
            .ToListAsync(cancellationToken);

        if (dueItems.Count == 0)
        {
            return;
        }

        foreach (var item in dueItems)
        {
            var deleted = await storage.TryDeleteAsync(item.ObjectPath, cancellationToken);
            if (deleted)
            {
                db.PendingStorageCleanups.Remove(item);
                _logger.LogInformation("Successfully cleaned up orphaned storage object '{ObjectPath}'.", item.ObjectPath);
            }
            else
            {
                item.AttemptCount++;
                item.LastAttemptedAt = now;
                var backoffMinutes = Math.Min(60 * 24, (int)Math.Pow(2, item.AttemptCount) * 5);
                item.NextRetryAt = now.AddMinutes(backoffMinutes);
                item.LastError = "Retry deletion returned failure.";
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
