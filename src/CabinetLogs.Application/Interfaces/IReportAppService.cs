using CabinetLogs.Application.DTOs.Reports;

namespace CabinetLogs.Application.Interfaces;

public interface IReportAppService
{
    Task<GeneratedFileResult> GenerateCabinetExcelReportAsync(
        Guid cabinetId,
        CabinetExcelReportQuery query,
        CancellationToken cancellationToken = default);
}
