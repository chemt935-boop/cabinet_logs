using CabinetLogs.Application.Interfaces;
using CabinetLogs.Infrastructure.Persistence;
using CabinetLogs.Infrastructure.Storage;
using CabinetLogs.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CabinetLogs.Tests.Infrastructure;

public sealed class CabinetLogsWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private readonly Dictionary<string, string?> _configOverrides;

    public FakePhotoStorageService FakeStorage { get; } = new();

    public CabinetLogsWebApplicationFactory(Dictionary<string, string?>? configOverrides = null)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _configOverrides = configOverrides ?? new Dictionary<string, string?>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var defaults = new Dictionary<string, string?>
            {
                ["Scalar:Enabled"] = "true",
                ["PhotoUpload:MaxFileSizeBytes"] = "1048576", // 1 MB for fast tests
                ["ApiSecurity:RequireApiKey"] = "false"
            };

            foreach (var kv in _configOverrides)
            {
                defaults[kv.Key] = kv.Value;
            }

            configBuilder.AddInMemoryCollection(defaults);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<CabinetLogsDbContext>>();
            services.RemoveAll<DbContextOptions<CabinetLogsDbContext>>();
            services.RemoveAll<CabinetLogsDbContext>();
            services.RemoveAll<IApplicationDbContext>();
            services.RemoveAll<IPhotoStorageService>();

            // Remove background worker during short-lived tests
            var hostedDescriptors = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                    && d.ImplementationType == typeof(StorageCleanupBackgroundService))
                .ToList();
            foreach (var d in hostedDescriptors)
            {
                services.Remove(d);
            }

            services.AddDbContext<CabinetLogsDbContext>(options => options.UseSqlite(_connection));
            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<CabinetLogsDbContext>());
            services.AddSingleton<IPhotoStorageService>(FakeStorage);
        });
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CabinetLogsDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
