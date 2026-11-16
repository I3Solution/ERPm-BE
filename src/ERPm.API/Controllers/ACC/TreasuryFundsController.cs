using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.DTOs.Accounting;
using ERPm.Application.Features.Accounting;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.ACC;

[Route("api/v1/accounting/treasury")]
[Route("api/v1/treasury")]
public class TreasuryFundsController(ITreasuryFundService treasuryService) : BaseApiController
{
    [RequirePermission("acc:treasury:view")]
    [HttpGet("balances")]
    public async Task<ActionResult<ApiResponse<TreasuryBalanceSummaryDto>>> GetBalances(
        [FromQuery] int? branchId,
        CancellationToken ct)
    {
        var result = await treasuryService.GetBalancesAsync(branchId, ct);
        return Ok(ApiResponse<TreasuryBalanceSummaryDto>.Ok(result));
    }

    [RequirePermission("acc:treasury:view")]
    [HttpGet("cash-funds")]
    public async Task<ActionResult<ApiResponse<List<CashFundDto>>>> GetCashFunds(
        [FromQuery] int? branchId,
        CancellationToken ct)
    {
        var result = await treasuryService.GetCashFundsAsync(branchId, ct);
        return Ok(ApiResponse<List<CashFundDto>>.Ok(result));
    }

    [RequirePermission("acc:treasury:view")]
    [HttpGet("cash-funds/{id:int}")]
    public async Task<ActionResult<ApiResponse<CashFundDto>>> GetCashFund(int id, CancellationToken ct)
    {
        var result = await treasuryService.GetCashFundAsync(id, ct);
        return result == null ? NotFound() : Ok(ApiResponse<CashFundDto>.Ok(result));
    }

    [RequirePermission("acc:treasury:create")]
    [HttpPost("cash-funds")]
    public async Task<ActionResult<ApiResponse<CashFundDto>>> CreateCashFund(
        [FromBody] CreateCashFundRequest request,
        CancellationToken ct)
    {
        var result = await treasuryService.CreateCashFundAsync(request, ct);
        return Ok(ApiResponse<CashFundDto>.Ok(result, "common.created"));
    }

    [RequirePermission("acc:treasury:edit")]
    [HttpPut("cash-funds/{id:int}")]
    public async Task<ActionResult<ApiResponse<CashFundDto>>> UpdateCashFund(
        int id,
        [FromBody] UpdateCashFundRequest request,
        CancellationToken ct)
    {
        var result = await treasuryService.UpdateCashFundAsync(id, request, ct);
        return Ok(ApiResponse<CashFundDto>.Ok(result, "common.updated"));
    }

    [RequirePermission("acc:treasury:delete")]
    [HttpDelete("cash-funds/{id:int}")]
    public async Task<ActionResult<ApiResponse<object?>>> DeleteCashFund(int id, CancellationToken ct)
    {
        await treasuryService.DeleteCashFundAsync(id, ct);
        return Ok(ApiResponse<object?>.Ok(null, "common.deleted"));
    }

    [RequirePermission("acc:treasury:view")]
    [HttpGet("bank-accounts")]
    public async Task<ActionResult<ApiResponse<List<BankAccountDto>>>> GetBankAccounts(
        [FromQuery] int? branchId,
        CancellationToken ct)
    {
        var result = await treasuryService.GetBankAccountsAsync(branchId, ct);
        return Ok(ApiResponse<List<BankAccountDto>>.Ok(result));
    }

    [RequirePermission("acc:treasury:view")]
    [HttpGet("bank-accounts/{id:int}")]
    public async Task<ActionResult<ApiResponse<BankAccountDto>>> GetBankAccount(int id, CancellationToken ct)
    {
        var result = await treasuryService.GetBankAccountAsync(id, ct);
        return result == null ? NotFound() : Ok(ApiResponse<BankAccountDto>.Ok(result));
    }

    [RequirePermission("acc:treasury:create")]
    [HttpPost("bank-accounts")]
    public async Task<ActionResult<ApiResponse<BankAccountDto>>> CreateBankAccount(
        [FromBody] CreateBankAccountRequest request,
        CancellationToken ct)
    {
        var result = await treasuryService.CreateBankAccountAsync(request, ct);
        return Ok(ApiResponse<BankAccountDto>.Ok(result, "common.created"));
    }

    [RequirePermission("acc:treasury:edit")]
    [HttpPut("bank-accounts/{id:int}")]
    public async Task<ActionResult<ApiResponse<BankAccountDto>>> UpdateBankAccount(
        int id,
        [FromBody] UpdateBankAccountRequest request,
        CancellationToken ct)
    {
        var result = await treasuryService.UpdateBankAccountAsync(id, request, ct);
        return Ok(ApiResponse<BankAccountDto>.Ok(result, "common.updated"));
    }

    [RequirePermission("acc:treasury:delete")]
    [HttpDelete("bank-accounts/{id:int}")]
    public async Task<ActionResult<ApiResponse<object?>>> DeleteBankAccount(int id, CancellationToken ct)
    {
        await treasuryService.DeleteBankAccountAsync(id, ct);
        return Ok(ApiResponse<object?>.Ok(null, "common.deleted"));
    }

    [RequirePermission("acc:treasury:create")]
    [HttpPost("internal-transfer")]
    public async Task<ActionResult<ApiResponse<InternalTransferResultDto>>> TransferInternal(
        [FromBody] InternalTransferRequest request,
        CancellationToken ct)
    {
        var result = await treasuryService.TransferInternalAsync(request, ct);
        return Ok(ApiResponse<InternalTransferResultDto>.Ok(result, "common.saved"));
    }
}
