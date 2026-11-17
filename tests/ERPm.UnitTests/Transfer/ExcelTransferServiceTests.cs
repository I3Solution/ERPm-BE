using ClosedXML.Excel;
using ERPm.Application.Common.Transfer;
using ERPm.Infrastructure.Services.Common.Transfer;
using Xunit;

namespace ERPm.UnitTests.Transfer;

public sealed class ExcelTransferServiceTests
{
    private readonly ExcelExportService _export = new();
    private readonly ExcelImportService _import = new();

    private sealed class SampleRow
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    [Fact]
    public void Export_And_Import_RoundTrip_PreservesUtf8AndNumericValues()
    {
        var rows = new List<SampleRow>
        {
            new() { Code = "KH001", Name = "Công ty Cổ phần Thương mại Á Châu", Amount = 12_500_000.50m },
            new() { Code = "KH002", Name = "Nguyễn Văn An", Amount = 0m }
        };

        var exported = _export.Export(new ExcelExportRequest<SampleRow>
        {
            SheetName = "Customers",
            Columns =
            [
                new ExportColumn<SampleRow> { Header = "Code", ValueSelector = r => r.Code },
                new ExportColumn<SampleRow> { Header = "Name", ValueSelector = r => r.Name },
                new ExportColumn<SampleRow> { Header = "Amount", ValueSelector = r => r.Amount, NumberFormat = "#,##0.00", Alignment = ExportAlignment.Right }
            ],
            Rows = rows
        }, "customers");

        Assert.Equal("customers.xlsx", exported.FileName);
        Assert.Equal(ExcelExportService.ExcelContentType, exported.ContentType);
        Assert.NotEmpty(exported.Content);

        using var stream = new MemoryStream(exported.Content);
        var rules = BuildRules();
        var imported = _import.Parse(stream, rules);

        Assert.True(imported.IsSuccess);
        Assert.Equal(2, imported.TotalRows);
        Assert.Equal(2, imported.ValidCount);
        Assert.Equal(0, imported.ErrorCount);
        Assert.Equal("KH001", imported.Items[0].Code);
        Assert.Equal("Công ty Cổ phần Thương mại Á Châu", imported.Items[0].Name);
        Assert.Equal(12_500_000.50m, imported.Items[0].Amount);
    }

    [Fact]
    public void Export_WithTitleSubtitleAndSummaryRow_WritesAllSections()
    {
        var rows = new List<SampleRow>
        {
            new() { Code = "INV-1", Name = "A", Amount = 100m },
            new() { Code = "INV-2", Name = "B", Amount = 250m }
        };

        var exported = _export.Export(new ExcelExportRequest<SampleRow>
        {
            SheetName = "AR Aging",
            Title = "Báo cáo Tuổi nợ",
            Subtitle = "Tính đến ngày 2026-11-17",
            Columns =
            [
                new ExportColumn<SampleRow> { Header = "Code", ValueSelector = r => r.Code },
                new ExportColumn<SampleRow> { Header = "Name", ValueSelector = r => r.Name },
                new ExportColumn<SampleRow> { Header = "Amount", ValueSelector = r => r.Amount }
            ],
            Rows = rows,
            SummaryValues = new Dictionary<int, object?>
            {
                [0] = "Tổng cộng",
                [2] = 350m
            }
        }, "ar-aging.xlsx");

        using var stream = new MemoryStream(exported.Content);
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet("AR Aging");

        Assert.Equal("Báo cáo Tuổi nợ", ws.Cell(1, 1).GetString());
        Assert.Equal("Tính đến ngày 2026-11-17", ws.Cell(2, 1).GetString());
        Assert.Equal("Code", ws.Cell(4, 1).GetString());
        Assert.Equal("Tổng cộng", ws.Cell(7, 1).GetString());
        Assert.Equal(350m, ws.Cell(7, 3).GetValue<decimal>());
    }

    [Fact]
    public void ExportTemplate_GeneratesHeaderOnlyWorkbook()
    {
        var template = _export.ExportTemplate("Accounts", ["Code", "Name", "Amount"], "accounts-template");
        using var stream = new MemoryStream(template.Content);
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet("Accounts");

        Assert.Equal("Code", ws.Cell(1, 1).GetString());
        Assert.Equal("Name", ws.Cell(1, 2).GetString());
        Assert.Equal("Amount", ws.Cell(1, 3).GetString());
        Assert.Equal(1, ws.LastRowUsed()?.RowNumber());
    }

    [Fact]
    public void Import_CollectsRowErrors_And_MissingRequiredHeaders()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Data");
        ws.Cell(1, 1).Value = "Code";
        ws.Cell(1, 2).Value = "Name";
        ws.Cell(1, 3).Value = "Amount";

        ws.Cell(2, 1).Value = "KH001";
        ws.Cell(2, 2).Value = "Valid";
        ws.Cell(2, 3).Value = "100";

        ws.Cell(3, 1).Value = "";
        ws.Cell(3, 2).Value = "Missing Code";
        ws.Cell(3, 3).Value = "not-a-number";

        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        var result = _import.Parse(ms, BuildRules());
        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ValidCount);
        Assert.Equal(2, result.ErrorCount);
        Assert.Contains(result.Errors, e => e.RowNumber == 3 && e.Column == "Code" && e.ErrorCode == "error.import.required");
        Assert.Contains(result.Errors, e => e.RowNumber == 3 && e.Column == "Amount" && e.ErrorCode == "error.import.invalid_decimal");
    }

    [Fact]
    public void Import_InvalidStream_ReturnsInvalidFileError()
    {
        using var ms = new MemoryStream("not-an-excel-file"u8.ToArray());
        var result = _import.Parse(ms, BuildRules());

        Assert.False(result.IsSuccess);
        Assert.Single(result.Errors);
        Assert.Equal("error.import.invalid_file", result.Errors[0].ErrorCode);
    }

    private static IReadOnlyList<ImportColumnRule<SampleRow>> BuildRules() =>
    [
        ImportColumnRule<SampleRow>.Create("Code", required: true, (r, v) => r.Code = v ?? string.Empty),
        ImportColumnRule<SampleRow>.Create("Name", required: true, (r, v) => r.Name = v ?? string.Empty),
        new ImportColumnRule<SampleRow>
        {
            Header = "Amount",
            Required = true,
            Apply = (raw, r) =>
            {
                if (!decimal.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var val))
                {
                    return new ImportRowError(0, "Amount", "error.import.invalid_decimal", "Invalid decimal.");
                }
                r.Amount = val;
                return null;
            }
        }
    ];
}
