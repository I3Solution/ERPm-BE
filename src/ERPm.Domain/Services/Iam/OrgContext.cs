namespace ERPm.Domain.Services.Iam;

/// <summary>Parent links of a self-referencing tree (branches or departments) with cached subtree lookups.</summary>
public sealed class OrgTree
{
    private readonly Dictionary<int, List<int>> _children = [];
    private readonly HashSet<int> _ids = [];

    public OrgTree(IEnumerable<(int Id, int? ParentId)> nodes)
    {
        foreach (var (id, parentId) in nodes)
        {
            _ids.Add(id);
            if (parentId is not { } parent) continue;
            if (!_children.TryGetValue(parent, out var list)) _children[parent] = list = [];
            list.Add(id);
        }
    }

    public bool Contains(int id) => _ids.Contains(id);

    /// <summary>The node and all of its descendants; empty when the node is unknown. Cycles are ignored.</summary>
    public IReadOnlySet<int> Subtree(int id)
    {
        var result = new HashSet<int>();
        if (!_ids.Contains(id)) return result;
        var stack = new Stack<int>([id]);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!result.Add(current)) continue;
            if (_children.TryGetValue(current, out var children)) foreach (var child in children) stack.Push(child);
        }
        return result;
    }
}

/// <summary>
/// Organization position of the signed-in account, used to turn relative data scopes
/// (SELF, BRANCH_TREE, COMPANY...) into concrete id sets.
/// </summary>
public sealed class OrgContext
{
    public string? UserKey { get; init; }                     // Value stored in CreatedBy for SELF scopes
    public int? CompanyId { get; init; }
    public int? BranchId { get; init; }
    public int? DepartmentId { get; init; }
    public string? DefaultScopeMode { get; init; }           // From the job position
    public IReadOnlyCollection<int> LegacyBranchIds { get; init; } = [];
    public OrgTree Branches { get; init; } = new([]);
    public OrgTree Departments { get; init; } = new([]);
    public IReadOnlyDictionary<int, int> DepartmentBranches { get; init; } = new Dictionary<int, int>();
}
