using ClosedXML.Excel;
using ERPm.Application.Common.Transfer;

namespace ERPm.Infrastructure.Services.Common.Transfer;

/// <summary>
/// ClosedXML-backed Excel (.xlsx) parser and row validator shared across all ERPm modules.
/// </summary>
public sealed class ExcelImportService : IExcelImportService
{
    public ImportResult<T> Parse<T>(
        Stream fileStream,
        IReadOnlyList<ImportColumnRule<T>> rules,
        string? sheetName = null,
        int maxRows = 10_000) where T : new()
    {
        ArgumentNullException.ThrowIfNull(fileStream);
        if (rules.Count == 0)
        {
            throw new ArgumentException("At least one import rule is required.", nameof(rules));
        }

        using var workbook = OpenWorkbook(fileStream);
        if (workbook is null)
        {
            return FailResult<T>(0, "error.import.invalid_file", "File is not a valid .xlsx workbook.");
        }

        var sheet = ResolveSheet(workbook, sheetName);
        if (sheet is null)
        {
            return FailResult<T>(0, "error.import.sheet_not_found", $"Worksheet '{sheetName}' was not found.");
        }

        var headerRow = sheet.FirstRowUsed();
        if (headerRow is null)
        {
            return FailResult<T>(0, "error.import.empty_file", "The uploaded worksheet is empty.");
        }

        var columnMap = MapHeaders(headerRow);
        var missingErrors = ValidateRequiredHeaders(rules, columnMap, headerRow.RowNumber());
        if (missingErrors.Count > 0)
        {
            return new ImportResult<T> { TotalRows = 0, Errors = missingErrors };
        }

        return ParseDataRows(sheet, headerRow.RowNumber(), rules, columnMap, maxRows);
    }

    private static XLWorkbook? OpenWorkbook(Stream stream)
    {
        try
        {
            if (stream.CanSeek) stream.Position = 0;
            return new XLWorkbook(stream);
        }
        catch
        {
            return null;
        }
    }

    private static IXLWorksheet? ResolveSheet(XLWorkbook workbook, string? sheetName)
    {
        if (string.IsNullOrWhiteSpace(sheetName))
        {
            return workbook.Worksheets.FirstOrDefault();
        }
        return workbook.Worksheets.FirstOrDefault(w =>
            string.Equals(w.Name, sheetName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, int> MapHeaders(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var text = cell.GetString().Trim();
            if (!string.IsNullOrEmpty(text) && !map.ContainsKey(text))
            {
                map[text] = cell.Address.ColumnNumber;
            }
        }
        return map;
    }

    private static List<ImportRowError> ValidateRequiredHeaders<T>(
        IReadOnlyList<ImportColumnRule<T>> rules,
        IReadOnlyDictionary<string, int> columnMap,
        int headerRowNumber)
    {
        var errors = new List<ImportRowError>();
        foreach (var rule in rules.Where(r => r.Required))
        {
            if (!columnMap.ContainsKey(rule.Header.Trim()))
            {
                errors.Add(new ImportRowError(
                    headerRowNumber,
                    rule.Header,
                    "error.import.missing_column",
                    $"Required column '{rule.Header}' is missing."));
            }
        }
        return errors;
    }

    private static ImportResult<T> ParseDataRows<T>(
        IXLWorksheet sheet,
        int headerRowNumber,
        IReadOnlyList<ImportColumnRule<T>> rules,
        IReadOnlyDictionary<string, int> columnMap,
        int maxRows) where T : new()
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRowNumber;
        var items = new List<T>();
        var errors = new List<ImportRowError>();
        var totalRows = 0;

        for (var r = headerRowNumber + 1; r <= lastRow; r++)
        {
            var row = sheet.Row(r);
            if (IsRowEmpty(row, columnMap.Values)) continue;

            totalRows++;
            if (totalRows > maxRows)
            {
                errors.Add(new ImportRowError(r, null, "error.import.max_rows_exceeded",
                    $"Maximum import limit of {maxRows} rows exceeded."));
                break;
            }

            var item = new T();
            var rowHasError = false;

            foreach (var rule in rules)
            {
                var raw = columnMap.TryGetValue(rule.Header.Trim(), out var colIndex)
                    ? row.Cell(colIndex).GetFormattedString()
                    : null;

                var err = rule.Apply(raw, item);
                if (err is not null)
                {
                    rowHasError = true;
                    errors.Add(err with { RowNumber = r });
                }
            }

            if (!rowHasError)
            {
                items.Add(item);
            }
        }

        return new ImportResult<T>
        {
            TotalRows = totalRows,
            Items = items,
            Errors = errors
        };
    }

    private static bool IsRowEmpty(IXLRow row, IEnumerable<int> mappedColumns) =>
        mappedColumns.All(col => string.IsNullOrWhiteSpace(row.Cell(col).GetFormattedString()));

    private static ImportResult<T> FailResult<T>(int row, string code, string message) => new()
    {
        TotalRows = 0,
        Errors = [new ImportRowError(row, null, code, message)]
    };
}
