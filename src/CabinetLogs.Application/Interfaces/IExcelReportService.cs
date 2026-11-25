using CabinetLogs.Application.DTOs.Reports;

namespace CabinetLogs.Application.Interfaces;

/// <summary>
/// Isolated Excel workbook generator service.
/// When the final designed Excel template is supplied later, update the implementation
/// of this interface without changing log CRUD or controllers.
/// </summary>
public interface IExcelReportService
{
    Task<GeneratedFileResult> GenerateCabinetReportAsync(
        ExcelReportData reportData,
        CancellationToken cancellationToken = default);
}
