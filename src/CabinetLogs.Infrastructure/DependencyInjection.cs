using CabinetLogs.Application.Common.Options;
using CabinetLogs.Application.Interfaces;
using CabinetLogs.Infrastructure.Persistence;
using CabinetLogs.Infrastructure.Reports;
using CabinetLogs.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CabinetLogs.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SupabaseOptions>(configuration.GetSection(SupabaseOptions.SectionName));
        services.Configure<PhotoUploadOptions>(configuration.GetSection(PhotoUploadOptions.SectionName));
        services.Configure<ApiSecurityOptions>(configuration.GetSection(ApiSecurityOptions.SectionName));

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<CabinetLogsDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(CabinetLogsDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            });
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<CabinetLogsDbContext>());
        services.AddSingleton<IDatabaseErrorInspector, PostgresDatabaseErrorInspector>();

        services.AddHttpClient<IPhotoStorageService, SupabaseStorageService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddScoped<IExcelReportService, ClosedXmlExcelReportService>();
        services.AddHostedService<StorageCleanupBackgroundService>();

        return services;
    }
}
