namespace ERPm.Domain.Services.Iam;

/// <summary>One grantable action in <c>module:resource:verb</c> form.</summary>
public sealed record PermissionDefinition(string Action, string Module, string Resource, string Verb);

/// <summary>
/// Every action the API checks with <c>[RequirePermission]</c>. Policies may only reference actions
/// (or wildcard patterns) that match at least one entry here.
/// </summary>
public static class PermissionCatalog
{
    public const string View = "view";
    public const string Create = "create";
    public const string Edit = "edit";
    public const string Delete = "delete";
    public const string Confirm = "confirm";
    public const string Cancel = "cancel";
    public const string ApproveCredit = "approve_credit";
    public const string Submit = "submit";
    public const string Withdraw = "withdraw";
    public const string Approve = "approve";
    public const string Reject = "reject";
    public const string Activate = "activate";
    public const string Close = "close";
    public const string Archive = "archive";
    public const string Broadcast = "broadcast";
    public const string UnlimitedCredit = "unlimited-credit";

    private static readonly string[] PriceListVerbs =
        [View, Create, Edit, Delete, Submit, Withdraw, Approve, Reject, Activate, Close, Archive];

    private static readonly (string Resource, string[] Verbs)[] Resources =
    [
        ("sys:branch", [View, Create, Edit, Delete]),
        ("sys:localization", [Edit]),
        ("sys:user", [View, Create, Edit]),
        ("sys:role", [View, Create, Edit, Delete]),
        ("sys:notification", [View, Edit, Broadcast]),
        ("hrm:department", [View, Create, Edit, Delete]),
        ("hrm:job-position", [View, Create, Edit, Delete]),
        ("hrm:employee", [View, Create, Edit, Delete]),
        ("hrm:work-history", [View, Create, Edit, Delete]),
        ("wms:item", [View, Create, Edit, Delete]),
        ("wms:category", [View, Create, Edit, Delete]),
        ("wms:item-attribute", [View, Create, Edit, Delete]),
        ("wms:uom", [View, Create, Edit, Delete]),
        ("wms:stock", [View]),
        ("wms:receipt", [Create]),
        ("sd:pricelist", PriceListVerbs),
        ("sd:quotation", [View, Create, Edit, Delete]),
        ("sd:order", [View, Create, Edit, Delete, Confirm, ApproveCredit, Cancel]),
        ("sd:invoice", [View, Create, Cancel]),
        ("sd:return", [View, Create]),
        ("sd:report", [View]),
        ("pur:pricelist", PriceListVerbs),
        ("pur:order", [View, Create, Edit, Delete, Confirm]),
        ("crm:partner", [View, Create, UnlimitedCredit]),
        ("acc:account", [View, Create, Edit]),
        ("acc:posting-profile", [View, Create, Edit]),
        ("acc:treasury", [View, Create, Edit, Delete]),
        ("acc:receipt-voucher", [View, Create, Cancel]),
        ("acc:payment-voucher", [View, Create, Cancel]),
        ("acc:report", [View]),
        ("dashboard:metrics", [View]),
    ];

    public static readonly IReadOnlyList<PermissionDefinition> All = Resources
        .SelectMany(r => r.Verbs.Select(v =>
        {
            var parts = r.Resource.Split(':');
            return new PermissionDefinition($"{r.Resource}:{v}", parts[0], parts[1], v);
        }))
        .ToList();

    private static readonly HashSet<string> Actions = All.Select(p => p.Action).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool Contains(string action) => Actions.Contains(action);

    /// <summary>Catalog actions matched by an action pattern such as <c>sd:*</c> or <c>*:view</c>.</summary>
    public static IEnumerable<string> Expand(string pattern) =>
        All.Select(p => p.Action).Where(a => IamPolicyEvaluator.Matches(pattern, a));
}
