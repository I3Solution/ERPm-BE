using ERPm.Application.Common;
using ERPm.Application.DTOs.Accounting;
using ERPm.Application.Features.Accounting;
using ERPm.Domain.Entities.Accounting;
using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services.Accounting;

public sealed class AccountService(ApplicationDbContext context) : IAccountService
{
    public async Task<List<AccountDto>> GetAccountsAsync(
        bool? isPostable = null,
        bool? isActive = null,
        bool? flat = null,
        CancellationToken ct = default)
    {
        var query = context.Accounts.AsNoTracking().AsQueryable();

        if (isActive.HasValue)
            query = query.Where(a => a.IsActive == isActive.Value);

        if (isPostable.HasValue)
            query = query.Where(a => a.IsPostable == isPostable.Value);

        var accounts = await query
            .OrderBy(a => a.Code)
            .ThenBy(a => a.Id)
            .ToListAsync(ct);

        var returnFlat = flat ?? isPostable.HasValue;
        if (returnFlat)
            return accounts.Select(Map).ToList();

        var dtoById = accounts.ToDictionary(a => a.Id, Map);
        var roots = new List<AccountDto>();

        foreach (var account in accounts)
        {
            var dto = dtoById[account.Id];
            if (account.ParentId.HasValue && dtoById.TryGetValue(account.ParentId.Value, out var parentDto))
                parentDto.Children.Add(dto);
            else
                roots.Add(dto);
        }

        return roots;
    }

    public async Task<AccountDto> CreateAccountAsync(CreateAccountRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            throw new BusinessException("error.validation.request", 400);

        if (!Enum.IsDefined(request.AccountType) || !Enum.IsDefined(request.Nature))
            throw new BusinessException("error.validation.request", 400);

        var code = request.Code.Trim();
        var exists = await context.Accounts.AnyAsync(a => a.Code == code, ct);
        if (exists)
            throw new BusinessException("error.accounting.account_code_exists", 409);

        if (request.ParentId.HasValue)
        {
            var parentExists = await context.Accounts.AnyAsync(a => a.Id == request.ParentId.Value, ct);
            if (!parentExists)
                throw new BusinessException("error.validation.reference", 404);
        }

        var entity = new Account
        {
            Code = code,
            Name = request.Name.Trim(),
            Translations = request.Translations ?? new Dictionary<string, string>(),
            AccountType = request.AccountType,
            Nature = request.Nature,
            IsPostable = request.IsPostable,
            TrackPartner = request.TrackPartner,
            TrackBranch = request.TrackBranch,
            ParentId = request.ParentId,
            IsActive = request.IsActive
        };

        context.Accounts.Add(entity);
        await context.SaveChangesAsync(ct);

        return Map(entity);
    }

    public async Task<AccountDto> UpdateAccountAsync(int id, UpdateAccountRequest request, CancellationToken ct = default)
    {
        if (id <= 0 || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            throw new BusinessException("error.validation.request", 400);

        if (!Enum.IsDefined(request.AccountType) || !Enum.IsDefined(request.Nature))
            throw new BusinessException("error.validation.request", 400);

        var entity = await context.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new BusinessException("error.accounting.accounts_missing", 404);

        var hasPostings = await context.JournalEntryLines.AnyAsync(l => l.AccountId == id, ct);
        if (hasPostings)
            throw new BusinessException("error.accounting.account_in_use", 409);

        var code = request.Code.Trim();
        var codeExists = await context.Accounts.AnyAsync(a => a.Id != id && a.Code == code, ct);
        if (codeExists)
            throw new BusinessException("error.accounting.account_code_exists", 409);

        if (request.ParentId.HasValue)
        {
            if (request.ParentId.Value == id)
                throw new BusinessException("error.validation.request", 400);

            var parentExists = await context.Accounts.AnyAsync(a => a.Id == request.ParentId.Value, ct);
            if (!parentExists)
                throw new BusinessException("error.validation.reference", 404);
        }

        entity.Code = code;
        entity.Name = request.Name.Trim();
        if (request.Translations != null)
            entity.Translations = request.Translations;
        entity.AccountType = request.AccountType;
        entity.Nature = request.Nature;
        entity.IsPostable = request.IsPostable;
        entity.TrackPartner = request.TrackPartner;
        entity.TrackBranch = request.TrackBranch;
        entity.ParentId = request.ParentId;
        entity.IsActive = request.IsActive;

        await context.SaveChangesAsync(ct);

        return Map(entity);
    }

    private static AccountDto Map(Account a) => new()
    {
        Id = a.Id,
        Code = a.Code,
        Name = a.Name,
        Translations = a.Translations,
        AccountType = a.AccountType,
        Nature = a.Nature,
        IsPostable = a.IsPostable,
        TrackPartner = a.TrackPartner,
        TrackBranch = a.TrackBranch,
        ParentId = a.ParentId,
        IsActive = a.IsActive
    };
}
