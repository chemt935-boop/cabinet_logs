using CabinetLogs.Application.DTOs.Reports;
using CabinetLogs.Application.Interfaces;
using CabinetLogs.Infrastructure.Reports;
using Microsoft.AspNetCore.Mvc;

namespace CabinetLogs.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Tags("Reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportAppService _service;

    public ReportsController(IReportAppService service)
    {
        _service = service;
    }

    /// <summary>
    /// Generate and download an Excel (.xlsx) report for one selected cabinet and optional date/time range.
    /// Does not include photos, photo thumbnails, or photo URLs.
    /// </summary>
    [HttpGet("cabinets/{cabinetId:guid}/excel")]
    [Produces(ClosedXmlExcelReportService.ExcelContentType, "application/problem+json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, ClosedXmlExcelReportService.ExcelContentType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadCabinetExcelReport(
        Guid cabinetId,
        [FromQuery] CabinetExcelReportQuery query,
        CancellationToken cancellationToken)
    {
        var file = await _service.GenerateCabinetExcelReportAsync(cabinetId, query, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
