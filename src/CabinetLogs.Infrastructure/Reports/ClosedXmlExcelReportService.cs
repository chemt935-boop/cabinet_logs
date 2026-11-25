using System.Globalization;
using System.Text;
using CabinetLogs.Application.DTOs.Reports;
using CabinetLogs.Application.Interfaces;
using ClosedXML.Excel;

namespace CabinetLogs.Infrastructure.Reports;

/// <summary>
/// ClosedXML-based Excel report generator.
/// Isolated behind <see cref="IExcelReportService"/> so a custom designed .xlsx template
/// can be plugged in later without touching controllers or log CRUD services.
///
/// Per specification section 3.5:
/// - Contains ONLY logs for the selected cabinet.
/// - Includes cabinet name in the report title/header, log date/time, and issue/details.
/// - NEVER includes photos, photo thumbnails, or photo URLs.
/// </summary>
public sealed class ClosedXmlExcelReportService : IExcelReportService
{
    public const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public Task<GeneratedFileResult> GenerateCabinetReportAsync(
        ExcelReportData reportData,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // TODO: When the final designed Excel template (.xlsx) is supplied, load it here via:
        // using var workbook = new XLWorkbook(templatePath);
        // and map reportData fields into the template cells.
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Cabinet Logs");

        // Title & metadata block
        worksheet.Cell("A1").Value = $"Cabinet Maintenance Logs — {reportData.CabinetName}";
        worksheet.Range("A1:E1").Merge();
        worksheet.Cell("A1").Style.Font.Bold = true;
        worksheet.Cell("A1").Style.Font.FontSize = 15;
        worksheet.Cell("A1").Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
        worksheet.Cell("A1").Style.Font.FontColor = XLColor.White;
        worksheet.Row(1).Height = 28;
        worksheet.Cell("A1").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        worksheet.Cell("A2").Value = "Cabinet:";
        worksheet.Cell("A2").Style.Font.Bold = true;
        worksheet.Cell("B2").Value = reportData.CabinetName;

        worksheet.Cell("A3").Value = "Date Range (UTC):";
        worksheet.Cell("A3").Style.Font.Bold = true;
        worksheet.Cell("B3").Value = FormatDateRange(reportData.From, reportData.To);

        worksheet.Cell("A4").Value = "Generated At (UTC):";
        worksheet.Cell("A4").Style.Font.Bold = true;
        worksheet.Cell("B4").Value = reportData.GeneratedAtUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

        worksheet.Cell("A5").Value = "Total Logs:";
        worksheet.Cell("A5").Style.Font.Bold = true;
        worksheet.Cell("B5").Value = reportData.Logs.Count;

        // Table header row (row 7)
        const int headerRow = 7;
        string[] headers = ["#", "Occurred At (UTC)", "Issue / Details", "Created By (Device)", "Recorded At (UTC)"];

        for (var col = 0; col < headers.Length; col++)
        {
            var cell = worksheet.Cell(headerRow, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2F5597");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }
        worksheet.Row(headerRow).Height = 22;

        // Data rows
        var currentRow = headerRow + 1;
        if (reportData.Logs.Count == 0)
        {
            worksheet.Cell(currentRow, 1).Value = "No logs found for this cabinet in the selected date/time range.";
            worksheet.Range(currentRow, 1, currentRow, headers.Length).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.Italic = true;
            worksheet.Cell(currentRow, 1).Style.Font.FontColor = XLColor.DimGray;
            worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        else
        {
            for (var i = 0; i < reportData.Logs.Count; i++)
            {
                var log = reportData.Logs[i];

                worksheet.Cell(currentRow, 1).Value = i + 1;
                worksheet.Cell(currentRow, 2).Value = log.OccurredAt.UtcDateTime;
                worksheet.Cell(currentRow, 2).Style.DateFormat.Format = "yyyy-MM-dd HH:mm:ss";
                worksheet.Cell(currentRow, 3).Value = log.Issue;
                worksheet.Cell(currentRow, 3).Style.Alignment.WrapText = true;
                worksheet.Cell(currentRow, 4).Value = log.CreatedByDeviceName;
                worksheet.Cell(currentRow, 5).Value = log.CreatedAt.UtcDateTime;
                worksheet.Cell(currentRow, 5).Style.DateFormat.Format = "yyyy-MM-dd HH:mm:ss";

                var rowRange = worksheet.Range(currentRow, 1, currentRow, headers.Length);
                rowRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                if (i % 2 == 1)
                {
                    rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F5F9");
                }

                currentRow++;
            }
        }

        // Column widths & freeze header
        worksheet.Column(1).Width = 8;
        worksheet.Column(2).Width = 22;
        worksheet.Column(3).Width = 65;
        worksheet.Column(4).Width = 24;
        worksheet.Column(5).Width = 22;
        worksheet.SheetView.FreezeRows(headerRow);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);

        var safeCabinetName = SanitizeFileName(reportData.CabinetName);
        var dateStamp = reportData.GeneratedAtUtc.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var fileName = $"{safeCabinetName}_Logs_{dateStamp}.xlsx";

        return Task.FromResult(new GeneratedFileResult(ms.ToArray(), ExcelContentType, fileName));
    }

    private static string FormatDateRange(DateTimeOffset? from, DateTimeOffset? to)
    {
        if (!from.HasValue && !to.HasValue)
        {
            return "All dates";
        }

        var fromStr = from.HasValue
            ? from.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)
            : "Beginning";

        var toStr = to.HasValue
            ? to.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)
            : "Present";

        return $"{fromStr} to {toStr} (exclusive)";
    }

    private static string SanitizeFileName(string input)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(input.Length);

        foreach (var ch in input.Trim())
        {
            if (Array.IndexOf(invalidChars, ch) >= 0 || char.IsWhiteSpace(ch))
            {
                sb.Append('_');
            }
            else
            {
                sb.Append(ch);
            }
        }

        var sanitized = sb.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "Cabinet" : sanitized;
    }
}
