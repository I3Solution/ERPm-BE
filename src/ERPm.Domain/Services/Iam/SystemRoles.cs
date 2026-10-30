using ERPm.Domain.Entities.System;

namespace ERPm.Domain.Services.Iam;

/// <summary>
/// Code-defined roles. ADMIN / OPERATOR / VIEWER mirror the legacy <c>AppUser.Role</c> values so existing
/// accounts keep their rights; the templates are group-wide starting points companies can copy.
/// </summary>
public static class SystemRoles
{
    private static readonly string[] ViewActions =
        ["wms:*:view", "sd:*:view", "pur:*:view", "crm:*:view", "dashboard:*:view", "sys:branch:view"];

    // Operator writes that were open to the legacy "Write" policy (everything else was Admin only).
    private static readonly string[] OperatorActions =
        ["wms:receipt:create", "sd:order:confirm", "sd:invoice:create", "pur:order:confirm", "crm:partner:create"];

    public static IReadOnlyList<(string Code, string Name, IamPolicyDocument Policy)> Legacy { get; } =
    [
        (IamRole.AdminCode, "Administrator", Policy(Allow("AdminFullAccess", ["*"], new IamDataScope { Mode = DataScopeModes.Group }))),
        (IamRole.OperatorCode, "Operator", Policy(Allow("OperatorRead", ViewActions), Allow("OperatorWrite", OperatorActions))),
        (IamRole.ViewerCode, "Viewer", Policy(Allow("ViewerRead", ViewActions))),
    ];

    public static IReadOnlyList<(string Code, string Name, IamPolicyDocument Policy)> Templates { get; } =
    [
        ("SALES_ACCOUNTANT", "Sales accountant", Policy(
            Allow("SalesAccounting", ["sd:order:view", "sd:order:edit", "sd:invoice:view", "sd:invoice:create", "crm:partner:view"]),
            Deny("NoInvoiceCancel", ["sd:invoice:cancel", "sd:order:delete"]))),
        ("WAREHOUSE_ACCOUNTANT", "Warehouse accountant", Policy(
            Allow("WarehouseAccounting", ["wms:*:view", "wms:receipt:create", "pur:order:view"]))),
        ("BRANCH_DIRECTOR", "Branch director", Policy(
            Allow("BranchManagement", ["sd:*", "pur:*", "crm:*", "wms:*:view", "dashboard:*:view", "hrm:*:view"]))),
        ("SALES_STAFF", "Sales staff", Policy(
            Allow("OwnOrders", ["sd:order:view", "sd:order:create", "sd:order:edit", "crm:partner:view", "wms:item:view"], new IamDataScope { Mode = DataScopeModes.Self }))),
    ];

    public static IamStatement Allow(string sid, string[] actions, IamDataScope? scope = null) =>
        new() { Sid = sid, Effect = IamStatement.Allow, Actions = [.. actions], DataScope = scope };

    public static IamStatement Deny(string sid, string[] actions) =>
        new() { Sid = sid, Effect = IamStatement.Deny, Actions = [.. actions] };

    public static IamPolicyDocument Policy(params IamStatement[] statements) => new() { Statements = [.. statements] };
}
