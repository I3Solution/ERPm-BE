using ERPm.Domain.Services.Iam;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ERPm.API.Security;

/// <summary>
/// Declares one or more IAM actions an endpoint accepts (any matching action allows access). A value starting with ':'
/// (for example ":view") is relative and is completed with the controller's <see cref="PermissionResourceAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class RequirePermissionAttribute(string action, params string[] alternatives) : Attribute
{
    public string Action { get; } = action;
    public string[] Actions { get; } = [action, .. alternatives];
}

/// <summary>Resource prefix (<c>module:resource</c>) for the relative permissions of a controller.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class PermissionResourceAttribute(string resource) : Attribute
{
    public string Resource { get; } = resource;
}

/// <summary>HttpContext slots shared by the account middleware, the permission filter and <see cref="CurrentUser"/>.</summary>
public static class IamHttpContext
{
    public const string PrincipalKey = "iam.principal";
    public const string ScopeKey = "iam.scope";

    public static IamPrincipal? Principal(this HttpContext http) => http.Items[PrincipalKey] as IamPrincipal;
    public static EffectiveDataScope? Scope(this HttpContext http) => http.Items[ScopeKey] as EffectiveDataScope ?? http.Principal()?.ReadScope;
}

/// <summary>
/// Global authorization filter. Endpoints with <see cref="RequirePermissionAttribute"/> are checked against the
/// caller's IAM principal and the matching data scope is published for the query filters. Unannotated write
/// endpoints keep the legacy "Write" policy (Admin / Operator) as a fail-safe.
/// </summary>
public sealed class PermissionFilter(IAuthorizationService authorization) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null) return;
        var required = endpoint?.Metadata.GetOrderedMetadata<RequirePermissionAttribute>().LastOrDefault();
        if (required != null)
        {
            var resource = endpoint!.Metadata.GetMetadata<PermissionResourceAttribute>();
            var principal = context.HttpContext.Principal();
            if (principal == null) { context.Result = new ForbidResult(); return; }

            var allowed = false;
            var scope = EffectiveDataScope.None;
            foreach (var candidate in required.Actions)
            {
                var action = Resolve(candidate, resource);
                if (action == null) continue;
                var decision = principal.Evaluate(action);
                if (!decision.Allowed) continue;
                allowed = true;
                scope = scope.Union(decision.Scope);
            }

            if (!allowed) { context.Result = new ForbidResult(); return; }
            context.HttpContext.Items[IamHttpContext.ScopeKey] = scope;
            return;
        }
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method)) return;
        if (!(await authorization.AuthorizeAsync(context.HttpContext.User, "Write")).Succeeded)
            context.Result = new ForbidResult();
    }

    /// <summary>Completes a relative action; null (deny) when no resource is declared.</summary>
    public static string? Resolve(string action, PermissionResourceAttribute? resource) =>
        !action.StartsWith(':') ? action : resource == null ? null : resource.Resource + action;
}
