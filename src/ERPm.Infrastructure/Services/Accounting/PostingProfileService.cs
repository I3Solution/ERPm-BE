using ERPm.Application.Common;
using ERPm.Application.DTOs.Accounting;
using ERPm.Application.Features.Accounting;
using ERPm.Domain.Entities.Accounting;
using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services.Accounting;

public sealed class PostingProfileService(ApplicationDbContext context) : IPostingProfileService
{
    public async Task<List<PostingProfileDto>> GetProfilesAsync(
        string? module = null,
        string? transactionType = null,
        int? branchId = null,
        CancellationToken ct = default)
    {
        var query = context.AccountPostingProfiles
            .AsNoTracking()
            .Include(p => p.Branch)
            .Include(p => p.DebitAccount)
            .Include(p => p.CreditAccount)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(module))
        {
            var mod = module.Trim().ToUpperInvariant();
            query = query.Where(p => p.Module == mod);
        }

        if (!string.IsNullOrWhiteSpace(transactionType))
        {
            var txType = transactionType.Trim().ToUpperInvariant();
            query = query.Where(p => p.TransactionType == txType);
        }

        if (branchId.HasValue)
            query = query.Where(p => p.BranchId == branchId.Value || p.BranchId == null);

        return await query
            .OrderBy(p => p.Module)
            .ThenBy(p => p.TransactionType)
            .ThenBy(p => p.BranchId)
            .ThenBy(p => p.DimensionKey)
            .Select(p => new PostingProfileDto
            {
                Id = p.Id,
                Module = p.Module,
                TransactionType = p.TransactionType,
                BranchId = p.BranchId,
                BranchCode = p.Branch != null ? p.Branch.Code : null,
                BranchName = p.Branch != null ? p.Branch.Name : null,
                DimensionKey = p.DimensionKey,
                DebitAccountId = p.DebitAccountId,
                DebitAccountCode = p.DebitAccount != null ? p.DebitAccount.Code : string.Empty,
                DebitAccountName = p.DebitAccount != null ? p.DebitAccount.Name : string.Empty,
                CreditAccountId = p.CreditAccountId,
                CreditAccountCode = p.CreditAccount != null ? p.CreditAccount.Code : string.Empty,
                CreditAccountName = p.CreditAccount != null ? p.CreditAccount.Name : string.Empty,
                Description = p.Description,
                IsActive = p.IsActive
            })
            .ToListAsync(ct);
    }

    public async Task<PostingProfileDto> UpsertProfileAsync(UpsertPostingProfileRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Module) || string.IsNullOrWhiteSpace(request.TransactionType))
            throw new BusinessException("error.validation.request", 400);

        var mod = request.Module.Trim().ToUpperInvariant();
        var txType = request.TransactionType.Trim().ToUpperInvariant();
        var dim = string.IsNullOrWhiteSpace(request.DimensionKey) ? null : request.DimensionKey.Trim().ToUpperInvariant();

        if (request.BranchId.HasValue)
        {
            var branchExists = await context.Branches.AnyAsync(b => b.Id == request.BranchId.Value && b.IsActive, ct);
            if (!branchExists)
                throw new BusinessException("error.validation.reference", 404);
        }

        var accounts = await context.Accounts
            .AsNoTracking()
            .Where(a => (a.Id == request.DebitAccountId || a.Id == request.CreditAccountId) && a.IsActive)
            .ToDictionaryAsync(a => a.Id, ct);

        if (!accounts.TryGetValue(request.DebitAccountId, out var debitAccount) ||
            !accounts.TryGetValue(request.CreditAccountId, out var creditAccount))
        {
            throw new BusinessException("error.accounting.accounts_missing", 409);
        }

        if (!debitAccount.IsPostable || !creditAccount.IsPostable)
            throw new BusinessException("error.accounting.account_not_postable", 400);

        var entity = await context.AccountPostingProfiles
            .FirstOrDefaultAsync(p => p.Module == mod
                && p.TransactionType == txType
                && p.BranchId == request.BranchId
                && p.DimensionKey == dim, ct);

        if (entity == null)
        {
            entity = new AccountPostingProfile
            {
                Module = mod,
                TransactionType = txType,
                BranchId = request.BranchId,
                DimensionKey = dim
            };
            context.AccountPostingProfiles.Add(entity);
        }

        entity.DebitAccountId = request.DebitAccountId;
        entity.CreditAccountId = request.CreditAccountId;
        entity.Description = request.Description?.Trim();
        entity.IsActive = request.IsActive;

        await context.SaveChangesAsync(ct);

        var saved = await context.AccountPostingProfiles
            .AsNoTracking()
            .Include(p => p.Branch)
            .Include(p => p.DebitAccount)
            .Include(p => p.CreditAccount)
            .SingleAsync(p => p.Id == entity.Id, ct);

        return new PostingProfileDto
        {
            Id = saved.Id,
            Module = saved.Module,
            TransactionType = saved.TransactionType,
            BranchId = saved.BranchId,
            BranchCode = saved.Branch?.Code,
            BranchName = saved.Branch?.Name,
            DimensionKey = saved.DimensionKey,
            DebitAccountId = saved.DebitAccountId,
            DebitAccountCode = saved.DebitAccount?.Code ?? string.Empty,
            DebitAccountName = saved.DebitAccount?.Name ?? string.Empty,
            CreditAccountId = saved.CreditAccountId,
            CreditAccountCode = saved.CreditAccount?.Code ?? string.Empty,
            CreditAccountName = saved.CreditAccount?.Name ?? string.Empty,
            Description = saved.Description,
            IsActive = saved.IsActive
        };
    }
}
