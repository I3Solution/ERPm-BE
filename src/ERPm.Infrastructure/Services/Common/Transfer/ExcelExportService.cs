using ClosedXML.Excel;
using ERPm.Application.Common.Transfer;

namespace ERPm.Infrastructure.Services.Common.Transfer;

/// <summary>
/// ClosedXML-backed Excel (.xlsx) generator shared across all ERPm modules.
/// </summary>
public sealed class ExcelExportService : IExcelExportService
{
    public const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public ExportFileResult Export<T>(ExcelExportRequest<T> request, string fileName)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Columns.Count == 0)
        {
            throw new ArgumentException("At least one export column is required.", nameof(request));
        }

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SanitizeSheetName(request.SheetName));
        var rowIndex = WriteTitleBlock(sheet, request.Title, request.Subtitle, request.Columns.Count);

        var headerRowIndex = rowIndex++;
        WriteHeaderRow(sheet, headerRowIndex, request.Columns.Select(c => c.Header).ToList());

        foreach (var item in request.Rows)
        {
            WriteDataRow(sheet, rowIndex++, request.Columns, item);
        }

        if (request.SummaryValues is { Count: > 0 })
        {
            WriteSummaryRow(sheet, rowIndex, request.Columns, request.SummaryValues);
        }

        AdjustColumnWidths(sheet, request.Columns.Count);
        return BuildResult(workbook, fileName);
    }

    public ExportFileResult ExportTemplate(string sheetName, IReadOnlyList<string> headers, string fileName)
    {
        if (headers.Count == 0)
        {
            throw new ArgumentException("At least one header is required.", nameof(headers));
        }

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SanitizeSheetName(sheetName));
        WriteHeaderRow(sheet, 1, headers);
        AdjustColumnWidths(sheet, headers.Count);
        return BuildResult(workbook, fileName);
    }

    private static int WriteTitleBlock(IXLWorksheet sheet, string? title, string? subtitle, int colCount)
    {
        var row = 1;
        if (!string.IsNullOrWhiteSpace(title))
        {
            var range = sheet.Range(row, 1, row, colCount).Merge();
            range.Value = title.Trim();
            range.Style.Font.Bold = true;
            range.Style.Font.FontSize = 14;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            row++;
        }

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            var range = sheet.Range(row, 1, row, colCount).Merge();
            range.Value = subtitle.Trim();
            range.Style.Font.Italic = true;
            range.Style.Font.FontColor = XLColor.DimGray;
            row++;
        }

        return row == 1 ? 1 : row + 1;
    }

    private static void WriteHeaderRow(IXLWorksheet sheet, int rowIndex, IReadOnlyList<string> headers)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            var cell = sheet.Cell(rowIndex, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2F7");
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
        sheet.SheetView.FreezeRows(rowIndex);
    }

    private static void WriteDataRow<T>(
        IXLWorksheet sheet,
        int rowIndex,
        IReadOnlyList<ExportColumn<T>> columns,
        T item)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            var cell = sheet.Cell(rowIndex, i + 1);
            AssignCellValue(cell, col.ValueSelector(item), col.NumberFormat, col.Alignment);
        }
    }

    private static void WriteSummaryRow<T>(
        IXLWorksheet sheet,
        int rowIndex,
        IReadOnlyList<ExportColumn<T>> columns,
        IReadOnlyDictionary<int, object?> summaryValues)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            var cell = sheet.Cell(rowIndex, i + 1);
            cell.Style.Font.Bold = true;
            cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
            if (summaryValues.TryGetValue(i, out var val))
            {
                AssignCellValue(cell, val, columns[i].NumberFormat, columns[i].Alignment);
            }
        }
    }

    private static void AssignCellValue(
        IXLCell cell,
        object? value,
        string? numberFormat,
        ExportAlignment alignment)
    {
        cell.Value = value switch
        {
            null => Blank.Value,
            string s => s,
            decimal d => d,
            double db => db,
            float f => (double)f,
            int n => n,
            long l => l,
            bool b => b,
            DateTime dt => dt,
            DateTimeOffset dto => dto.UtcDateTime,
            DateOnly dOnly => dOnly.ToDateTime(TimeOnly.MinValue),
            _ => value.ToString() ?? string.Empty
        };

        if (!string.IsNullOrWhiteSpace(numberFormat))
        {
            cell.Style.NumberFormat.Format = numberFormat;
        }

        cell.Style.Alignment.Horizontal = alignment switch
        {
            ExportAlignment.Center => XLAlignmentHorizontalValues.Center,
            ExportAlignment.Right => XLAlignmentHorizontalValues.Right,
            _ => XLAlignmentHorizontalValues.Left
        };
    }

    private static void AdjustColumnWidths(IXLWorksheet sheet, int colCount)
    {
        for (var i = 1; i <= colCount; i++)
        {
            var col = sheet.Column(i);
            col.AdjustToContents();
            col.Width = Math.Clamp(col.Width + 2, 12, 50);
        }
    }

    private static string SanitizeSheetName(string? sheetName)
    {
        var name = string.IsNullOrWhiteSpace(sheetName) ? "Data" : sheetName.Trim();
        foreach (var invalid in new[] { ':', '\\', '/', '?', '*', '[', ']' })
        {
            name = name.Replace(invalid, '_');
        }
        return name.Length > 31 ? name[..31] : name;
    }

    private static ExportFileResult BuildResult(XLWorkbook workbook, string fileName)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var safeName = fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"{fileName}.xlsx";
        return new ExportFileResult(stream.ToArray(), ExcelContentType, safeName);
    }
}
