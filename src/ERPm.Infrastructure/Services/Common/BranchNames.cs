using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services.Common;

/// <summary>
/// Branch names for document lists. Including the branch navigation would inner-join the branch table through its
/// data scope filter and hide every document of a seller who sees no branch (SELF scope), so names are looked up apart.
/// </summary>
internal static class BranchNames
{
    public static async Task<Dictionary<int, string>> ForAsync(ApplicationDbContext context, IEnumerable<int> branchIds, CancellationToken ct)
    {
        var ids = branchIds.Distinct().ToArray();
        return ids.Length == 0
            ? []
            : await context.Branches.IgnoreQueryFilters().Where(b => ids.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Name, ct);
    }
}
