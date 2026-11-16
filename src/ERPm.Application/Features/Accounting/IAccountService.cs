using ERPm.Application.DTOs.Accounting;

namespace ERPm.Application.Features.Accounting;

public interface IAccountService
{
    Task<List<AccountDto>> GetAccountsAsync(bool? isPostable = null, bool? isActive = null, bool? flat = null, CancellationToken ct = default);
    Task<AccountDto> CreateAccountAsync(CreateAccountRequest request, CancellationToken ct = default);
    Task<AccountDto> UpdateAccountAsync(int id, UpdateAccountRequest request, CancellationToken ct = default);
}
