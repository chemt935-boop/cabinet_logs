using System.Globalization;
using CabinetLogs.Application.Interfaces;
using CabinetLogs.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CabinetLogs.Infrastructure.Persistence;

public sealed class CabinetLogsDbContext : DbContext, IApplicationDbContext
{
    public CabinetLogsDbContext(DbContextOptions<CabinetLogsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Cabinet> Cabinets => Set<Cabinet>();
    public DbSet<ClientDevice> ClientDevices => Set<ClientDevice>();
    public DbSet<MaintenanceLog> Logs => Set<MaintenanceLog>();
    public DbSet<LogPhoto> LogPhotos => Set<LogPhoto>();
    public DbSet<PendingStorageCleanup> PendingStorageCleanups => Set<PendingStorageCleanup>();

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<IDbContextTransaction?, CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default)
    {
        if (!Database.IsRelational())
        {
            return await action(null, cancellationToken);
        }

        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            var result = await action(transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    public void Detach(object entity)
    {
        Entry(entity).State = EntityState.Detached;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CabinetLogsDbContext).Assembly);

        if (Database.IsNpgsql())
        {
            ConfigurePostgresDefaults(modelBuilder);
        }
        else if (Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            ConfigureSqliteDateTimeOffsetConverters(modelBuilder);
        }
    }

    private static void ConfigurePostgresDefaults(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cabinet>(b =>
        {
            b.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
            b.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
            b.Property(c => c.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<ClientDevice>(b =>
        {
            b.Property(d => d.Id).HasDefaultValueSql("gen_random_uuid()");
            b.Property(d => d.CreatedAt).HasDefaultValueSql("now()");
            b.Property(d => d.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<MaintenanceLog>(b =>
        {
            b.Property(l => l.Id).HasDefaultValueSql("gen_random_uuid()");
            b.Property(l => l.CreatedAt).HasDefaultValueSql("now()");
            b.Property(l => l.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<LogPhoto>(b =>
        {
            b.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
            b.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<PendingStorageCleanup>(b =>
        {
            b.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
            b.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
        });
    }

    private static void ConfigureSqliteDateTimeOffsetConverters(ModelBuilder modelBuilder)
    {
        var converter = new ValueConverter<DateTimeOffset, string>(
            v => v.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture),
            v => DateTimeOffset.Parse(v, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal));

        var nullableConverter = new ValueConverter<DateTimeOffset?, string?>(
            v => v.HasValue
                ? v.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture)
                : null,
            v => string.IsNullOrEmpty(v)
                ? null
                : DateTimeOffset.Parse(v, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(converter);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(nullableConverter);
                }
            }
        }
    }
}
