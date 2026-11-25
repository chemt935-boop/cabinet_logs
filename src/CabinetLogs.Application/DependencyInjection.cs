using CabinetLogs.Application.Interfaces;
using CabinetLogs.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CabinetLogs.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IClientDeviceService, ClientDeviceService>();
        services.AddScoped<ICabinetService, CabinetService>();
        services.AddScoped<ILogService, LogService>();
        services.AddScoped<IReportAppService, ReportAppService>();
        return services;
    }
}
