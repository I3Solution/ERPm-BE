using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.DTOs.Accounting;
using ERPm.Application.Features.Accounting;
using ERPm.Domain.Entities.Accounting;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.ACC;

[Route("api/v1/accounting/reports")]
public sealed class AccountingReportsController(IAccountingReportService reports) : BaseApiController
{
    [RequirePermission("acc:report:view")]
    [HttpGet("ar-aging")]
    public async Task<ActionResult<ApiResponse<ArAgingReportDto>>> GetArAging(
        [FromQuery] DateTime? asOfDate,
        [FromQuery] int? branchId,
        [FromQuery] int? customerId,
        CancellationToken ct)
    {
        var result = await reports.GetArAgingAsync(asOfDate, branchId, customerId, ct);
        return Ok(ApiResponse<ArAgingReportDto>.Ok(result));
    }

    [RequirePermission("acc:report:view")]
    [HttpGet("ar-aging/export")]
    public async Task<IActionResult> ExportArAging(
        [FromQuery] DateTime? asOfDate,
        [FromQuery] int? branchId,
        [FromQuery] int? customerId,
        CancellationToken ct)
    {
        var file = await reports.ExportArAgingAsync(asOfDate, branchId, customerId, ct);
        return ExcelFile(file);
    }

    [RequirePermission("acc:report:view")]
    [HttpGet("statement-of-account/{partnerId:int}")]
    public async Task<ActionResult<ApiResponse<StatementOfAccountDto>>> GetStatementOfAccount(
        int partnerId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? branchId,
        CancellationToken ct)
    {
        var result = await reports.GetStatementOfAccountAsync(partnerId, from, to, branchId, ct);
        return Ok(ApiResponse<StatementOfAccountDto>.Ok(result));
    }

    [RequirePermission("acc:report:view")]
    [HttpGet("general-journal")]
    public async Task<ActionResult<ApiResponse<GeneralJournalReportDto>>> GetGeneralJournal(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? branchId,
        [FromQuery] string? voucherType,
        CancellationToken ct)
    {
        var result = await reports.GetGeneralJournalAsync(from, to, branchId, voucherType, ct);
        return Ok(ApiResponse<GeneralJournalReportDto>.Ok(result));
    }

    [RequirePermission("acc:report:view")]
    [HttpGet("general-ledger/{accountId:int}")]
    public async Task<ActionResult<ApiResponse<GeneralLedgerReportDto>>> GetGeneralLedger(
        int accountId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? branchId,
        CancellationToken ct)
    {
        var result = await reports.GetGeneralLedgerAsync(accountId, from, to, branchId, ct);
        return Ok(ApiResponse<GeneralLedgerReportDto>.Ok(result));
    }

    [RequirePermission("acc:report:view")]
    [HttpGet("treasury-ledger")]
    public async Task<ActionResult<ApiResponse<TreasuryLedgerReportDto>>> GetTreasuryLedger(
        [FromQuery] TreasuryPaymentMethod method,
        [FromQuery] int fundOrBankId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var result = await reports.GetTreasuryLedgerAsync(method, fundOrBankId, from, to, ct);
        return Ok(ApiResponse<TreasuryLedgerReportDto>.Ok(result));
    }
}
