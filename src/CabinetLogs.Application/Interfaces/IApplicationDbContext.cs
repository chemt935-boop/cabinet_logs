using CabinetLogs.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace CabinetLogs.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Cabinet> Cabinets { get; }
    DbSet<ClientDevice> ClientDevices { get; }
    DbSet<MaintenanceLog> Logs { get; }
    DbSet<LogPhoto> LogPhotos { get; }
    DbSet<PendingStorageCleanup> PendingStorageCleanups { get; }

    DatabaseFacade Database { get; }

    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<IDbContextTransaction?, CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default);
    void Detach(object entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
