using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.DTOs.Accounting;
using ERPm.Application.Features.Accounting;
using Microsoft.AspNetCore.Mvc;
using AccountDto = ERPm.Application.DTOs.Accounting.AccountDto;

namespace ERPm.API.Controllers.ACC;

[Route("api/v1/accounting/accounts")]
public class AccountsController(IAccountService accountService) : BaseApiController
{
    [RequirePermission("acc:account:view")]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<AccountDto>>>> GetAll(
        [FromQuery] bool? isPostable,
        [FromQuery] bool? isActive,
        [FromQuery] bool? flat,
        CancellationToken ct)
    {
        var result = await accountService.GetAccountsAsync(isPostable, isActive, flat, ct);
        return Ok(ApiResponse<List<AccountDto>>.Ok(result));
    }

    [RequirePermission("acc:account:create")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<AccountDto>>> Create(
        [FromBody] CreateAccountRequest request,
        CancellationToken ct)
    {
        var result = await accountService.CreateAccountAsync(request, ct);
        return Ok(ApiResponse<AccountDto>.Ok(result, "common.created"));
    }

    [RequirePermission("acc:account:edit")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<AccountDto>>> Update(
        int id,
        [FromBody] UpdateAccountRequest request,
        CancellationToken ct)
    {
        var result = await accountService.UpdateAccountAsync(id, request, ct);
        return Ok(ApiResponse<AccountDto>.Ok(result, "common.updated"));
    }
}
