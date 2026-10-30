using ERPm.Domain.Entities.System;

namespace ERPm.Domain.Services.Iam;

/// <summary>
/// Resolved permissions of one account: the union of its job-position roles, extra user roles, legacy
/// system role and inline policy, together with its organization context.
/// </summary>
public sealed class IamPrincipal
{
    private readonly Dictionary<string, PermissionDecision> _decisions = new(StringComparer.OrdinalIgnoreCase);

    public IamPrincipal(IReadOnlyList<IamStatement> statements, OrgContext context, bool isSuperAdmin)
    {
        Statements = statements;
        Context = context;
        IsSuperAdmin = isSuperAdmin;
        ReadScope = IsSuperAdmin ? EffectiveDataScope.Everything : statements
            .Where(s => !IamPolicyEvaluator.IsDeny(s))
            .Aggregate(EffectiveDataScope.None, (scope, s) => scope.Union(IamPolicyEvaluator.Resolve(s.DataScope, context)));
    }

    public IReadOnlyList<IamStatement> Statements { get; }
    public OrgContext Context { get; }
    public bool IsSuperAdmin { get; }
    public int? EmployeeId { get; init; }
    public int? JobPositionId { get; init; }

    /// <summary>Union of every Allow scope; used when a request has no specific permission check.</summary>
    public EffectiveDataScope ReadScope { get; }

    public PermissionDecision Evaluate(string action)
    {
        if (IsSuperAdmin) return new(true, false, EffectiveDataScope.Everything);
        lock (_decisions)
        {
            if (!_decisions.TryGetValue(action, out var decision))
                _decisions[action] = decision = IamPolicyEvaluator.Evaluate(Statements, action, Context);
            return decision;
        }
    }

    public bool Can(string action) => Evaluate(action).Allowed;

    public IEnumerable<string> AllowedActions() => PermissionCatalog.All.Select(p => p.Action).Where(Can);

    /// <summary>
    /// Privilege escalation guard: lists every action or data range in <paramref name="granted"/> that this
    /// principal could not use itself. Deny statements only restrict and are always accepted.
    /// </summary>
    public IReadOnlyList<string> Escalations(IEnumerable<IamStatement> granted)
    {
        var problems = new List<string>();
        if (IsSuperAdmin) return problems;
        foreach (var statement in granted.Where(s => !IamPolicyEvaluator.IsDeny(s)))
        foreach (var action in statement.Actions.SelectMany(PermissionCatalog.Expand).Distinct())
        {
            var own = Evaluate(action);
            if (!own.Allowed) { problems.Add(action); continue; }
            if (own.Scope.Unrestricted) continue;
            var scope = statement.DataScope;
            var mode = scope?.Mode.ToUpperInvariant();
            var covered = mode switch
            {
                DataScopeModes.Group => false,
                DataScopeModes.Company => Context.CompanyId is { } company && Context.Branches.Subtree(company).All(own.Scope.BranchIds.Contains),
                _ => true
            };
            if (covered && scope != null)
            {
                var customBranches = scope.CustomBranchIds.SelectMany(id => scope.IncludeDescendants ? Context.Branches.Subtree(id) : new HashSet<int> { id });
                var customDepartments = scope.CustomDepartmentIds.SelectMany(id => scope.IncludeDescendants ? Context.Departments.Subtree(id) : new HashSet<int> { id });
                covered = (mode != DataScopeModes.CustomBranches || customBranches.All(own.Scope.BranchIds.Contains))
                    && customDepartments.All(d => own.Scope.DepartmentIds.Contains(d)
                        || (Context.DepartmentBranches.TryGetValue(d, out var branch) && own.Scope.BranchIds.Contains(branch)));
            }
            if (!covered) problems.Add($"{action}@{scope!.Mode}");
        }
        return problems;
    }
}
