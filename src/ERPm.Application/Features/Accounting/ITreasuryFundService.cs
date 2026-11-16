using ERPm.Application.DTOs.Accounting;

namespace ERPm.Application.Features.Accounting;

public interface ITreasuryFundService
{
    Task<List<CashFundDto>> GetCashFundsAsync(int? branchId = null, CancellationToken ct = default);
    Task<CashFundDto?> GetCashFundAsync(int id, CancellationToken ct = default);
    Task<CashFundDto> CreateCashFundAsync(CreateCashFundRequest request, CancellationToken ct = default);
    Task<CashFundDto> UpdateCashFundAsync(int id, UpdateCashFundRequest request, CancellationToken ct = default);
    Task DeleteCashFundAsync(int id, CancellationToken ct = default);

    Task<List<BankAccountDto>> GetBankAccountsAsync(int? branchId = null, CancellationToken ct = default);
    Task<BankAccountDto?> GetBankAccountAsync(int id, CancellationToken ct = default);
    Task<BankAccountDto> CreateBankAccountAsync(CreateBankAccountRequest request, CancellationToken ct = default);
    Task<BankAccountDto> UpdateBankAccountAsync(int id, UpdateBankAccountRequest request, CancellationToken ct = default);
    Task DeleteBankAccountAsync(int id, CancellationToken ct = default);

    Task<TreasuryBalanceSummaryDto> GetBalancesAsync(int? branchId = null, CancellationToken ct = default);
    Task<InternalTransferResultDto> TransferInternalAsync(InternalTransferRequest request, CancellationToken ct = default);
}
