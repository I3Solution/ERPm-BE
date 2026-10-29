using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ERPm.API.Security;

public sealed class WriteAccessFilter(IAuthorizationService authorization) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() != null) return;
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method)) return;
        if (!(await authorization.AuthorizeAsync(context.HttpContext.User, "Write")).Succeeded)
            context.Result = new ForbidResult();
    }
}
