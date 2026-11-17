namespace ERPm.Application.Common.Transfer;

/// <summary>
/// Core service for generating Excel (.xlsx) exports and blank import templates.
/// </summary>
public interface IExcelExportService
{
    ExportFileResult Export<T>(ExcelExportRequest<T> request, string fileName);
    ExportFileResult ExportTemplate(string sheetName, IReadOnlyList<string> headers, string fileName);
}

/// <summary>
/// Core service for parsing and validating Excel (.xlsx) uploads into typed DTOs.
/// </summary>
public interface IExcelImportService
{
    ImportResult<T> Parse<T>(
        Stream fileStream,
        IReadOnlyList<ImportColumnRule<T>> rules,
        string? sheetName = null,
        int maxRows = 10_000) where T : new();
}
