namespace ERPm.Application.Common.Transfer;

public enum ExportAlignment
{
    Left = 0,
    Center = 1,
    Right = 2
}

/// <summary>
/// Describes a single column in an Excel export sheet.
/// </summary>
public sealed class ExportColumn<T>
{
    public required string Header { get; init; }
    public required Func<T, object?> ValueSelector { get; init; }
    public string? NumberFormat { get; init; }
    public ExportAlignment Alignment { get; init; } = ExportAlignment.Left;
}

/// <summary>
/// Request payload for generating an Excel workbook from typed rows.
/// </summary>
public sealed class ExcelExportRequest<T>
{
    public string SheetName { get; init; } = "Data";
    public string? Title { get; init; }
    public string? Subtitle { get; init; }
    public required IReadOnlyList<ExportColumn<T>> Columns { get; init; }
    public required IReadOnlyList<T> Rows { get; init; }
    public IReadOnlyDictionary<int, object?>? SummaryValues { get; init; }
}

/// <summary>
/// Binary file result ready to be returned by an API controller.
/// </summary>
public sealed record ExportFileResult(
    byte[] Content,
    string ContentType,
    string FileName);

/// <summary>
/// Validation or parsing error for a single imported row/column.
/// </summary>
public sealed record ImportRowError(
    int RowNumber,
    string? Column,
    string ErrorCode,
    string Message);

/// <summary>
/// Aggregated result of parsing and validating an imported spreadsheet.
/// </summary>
public sealed class ImportResult<T>
{
    public int TotalRows { get; init; }
    public int ValidCount => Items.Count;
    public int ErrorCount => Errors.Count;
    public bool IsSuccess => Errors.Count == 0;
    public IReadOnlyList<T> Items { get; init; } = [];
    public IReadOnlyList<ImportRowError> Errors { get; init; } = [];
}

/// <summary>
/// Column mapping and validation rule for importing a typed row from Excel.
/// </summary>
public sealed class ImportColumnRule<T>
{
    public required string Header { get; init; }
    public bool Required { get; init; }
    public required Func<string?, T, ImportRowError?> Apply { get; init; }

    /// <summary>
    /// Helper to build a column rule from a simple value parser returning an error code on failure.
    /// </summary>
    public static ImportColumnRule<T> Create(
        string header,
        bool required,
        Action<T, string?> setter,
        Func<string?, string?>? validator = null) => new()
    {
        Header = header,
        Required = required,
        Apply = (raw, item) =>
        {
            var trimmed = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
            if (required && trimmed is null)
            {
                return new ImportRowError(0, header, "error.import.required", $"Column '{header}' is required.");
            }
            if (validator is not null)
            {
                var err = validator(trimmed);
                if (err is not null)
                {
                    return new ImportRowError(0, header, err, $"Invalid value '{trimmed}' for '{header}'.");
                }
            }
            setter(item, trimmed);
            return null;
        }
    };
}
