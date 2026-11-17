using ERPm.Application.Common.Transfer;
using ERPm.Application.DTOs.Accounting;
using ERPm.Domain.Entities.Accounting;

namespace ERPm.Application.Features.Accounting;

public interface IAccountingReportService
{
    Task<ArAgingReportDto> GetArAgingAsync(
        DateTime? asOfDate,
        int? branchId,
        int? customerId,
        CancellationToken ct = default);

    Task<ExportFileResult> ExportArAgingAsync(
        DateTime? asOfDate,
        int? branchId,
        int? customerId,
        CancellationToken ct = default);

    Task<StatementOfAccountDto> GetStatementOfAccountAsync(
        int partnerId,
        DateTime? from,
        DateTime? to,
        int? branchId,
        CancellationToken ct = default);

    Task<GeneralJournalReportDto> GetGeneralJournalAsync(
        DateTime? from,
        DateTime? to,
        int? branchId,
        string? voucherType,
        CancellationToken ct = default);

    Task<GeneralLedgerReportDto> GetGeneralLedgerAsync(
        int accountId,
        DateTime? from,
        DateTime? to,
        int? branchId,
        CancellationToken ct = default);

    Task<TreasuryLedgerReportDto> GetTreasuryLedgerAsync(
        TreasuryPaymentMethod method,
        int fundOrBankId,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default);
}
