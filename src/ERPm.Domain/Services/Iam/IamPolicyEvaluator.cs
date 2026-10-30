using ERPm.Domain.Entities.System;

namespace ERPm.Domain.Services.Iam;

/// <summary>
/// Pure IAM rules: wildcard matching, Default Deny, Explicit Deny wins, and union of the data scopes of
/// every matching Allow statement.
/// </summary>
public static class IamPolicyEvaluator
{
    /// <summary>Case-insensitive glob match where <c>*</c> spans any characters, including <c>:</c>.</summary>
    public static bool Matches(string pattern, string value)
    {
        int p = 0, v = 0, star = -1, mark = 0;
        while (v < value.Length)
        {
            if (p < pattern.Length && pattern[p] != '*' && char.ToLowerInvariant(pattern[p]) == char.ToLowerInvariant(value[v])) { p++; v++; }
            else if (p < pattern.Length && pattern[p] == '*') { star = p++; mark = v; }
            else if (star >= 0) { p = star + 1; v = ++mark; }
            else return false;
        }
        while (p < pattern.Length && pattern[p] == '*') p++;
        return p == pattern.Length;
    }

    public static bool Applies(IamStatement statement, string action, string resource = "*") =>
        statement.Actions.Any(a => Matches(a, action))
        && (statement.Resources.Count == 0 || statement.Resources.Any(r => r == "*" || Matches(r, resource)));

    public static PermissionDecision Evaluate(IEnumerable<IamStatement> statements, string action, OrgContext context, string resource = "*")
    {
        var matching = statements.Where(s => Applies(s, action, resource)).ToList();
        if (matching.Any(s => IsDeny(s))) return new(false, true, EffectiveDataScope.None);
        var allows = matching.Where(s => string.Equals(s.Effect, IamStatement.Allow, StringComparison.OrdinalIgnoreCase)).ToList();
        if (allows.Count == 0) return PermissionDecision.Denied;
        var scope = allows.Aggregate(EffectiveDataScope.None, (current, s) => current.Union(Resolve(s.DataScope, context)));
        return new(true, false, scope);
    }

    public static bool IsDeny(IamStatement statement) => string.Equals(statement.Effect, IamStatement.Deny, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Turns a statement scope into concrete ids for the holder. A missing scope uses the job position's
    /// default mode plus the legacy account branch grants.
    /// </summary>
    public static EffectiveDataScope Resolve(IamDataScope? scope, OrgContext context)
    {
        if (scope == null)
        {
            var legacy = new EffectiveDataScope(false, context.LegacyBranchIds.SelectMany(context.Branches.Subtree).ToHashSet(), new HashSet<int>(), false);
            return context.DefaultScopeMode is { } mode ? legacy.Union(Resolve(new IamDataScope { Mode = mode }, context)) : legacy;
        }

        var branches = new HashSet<int>();
        var departments = new HashSet<int>();
        var own = false;
        switch (scope.Mode.ToUpperInvariant())
        {
            case DataScopeModes.Group:
                return EffectiveDataScope.Everything;
            case DataScopeModes.Self:
                own = context.UserKey != null;
                break;
            case DataScopeModes.Department:
                if (context.DepartmentId is { } department) departments.Add(department);
                break;
            case DataScopeModes.DepartmentTree:
                if (context.DepartmentId is { } root) departments.UnionWith(context.Departments.Subtree(root));
                break;
            case DataScopeModes.Branch:
                if (context.BranchId is { } branch) branches.Add(branch);
                break;
            case DataScopeModes.BranchTree:
                if (context.BranchId is { } top) branches.UnionWith(context.Branches.Subtree(top));
                break;
            case DataScopeModes.Company:
                if (context.CompanyId is { } company) branches.UnionWith(context.Branches.Subtree(company));
                break;
            case DataScopeModes.CustomBranches:
                foreach (var id in scope.CustomBranchIds)
                    if (scope.IncludeDescendants) branches.UnionWith(context.Branches.Subtree(id));
                    else if (context.Branches.Contains(id)) branches.Add(id);
                break;
            default:
                return EffectiveDataScope.None;
        }
        foreach (var id in scope.CustomDepartmentIds)
            if (scope.IncludeDescendants) departments.UnionWith(context.Departments.Subtree(id));
            else if (context.Departments.Contains(id)) departments.Add(id);
        return new EffectiveDataScope(false, branches, departments, own);
    }

    /// <summary>
    /// Structural check of a policy document: known effects and modes, and every action pattern must match
    /// at least one catalog action. Returns the offending item or null when valid.
    /// </summary>
    public static string? Validate(IamPolicyDocument? document)
    {
        if (document == null) return null;
        foreach (var statement in document.Statements)
        {
            if (!IsDeny(statement) && !string.Equals(statement.Effect, IamStatement.Allow, StringComparison.OrdinalIgnoreCase)) return statement.Effect;
            if (statement.Actions.Count == 0) return statement.Sid ?? "Actions";
            foreach (var action in statement.Actions)
                if (string.IsNullOrWhiteSpace(action) || !PermissionCatalog.Expand(action).Any()) return action;
            if (statement.DataScope is { } scope && !DataScopeModes.All.Contains(scope.Mode.ToUpperInvariant())) return scope.Mode;
        }
        return null;
    }
}
