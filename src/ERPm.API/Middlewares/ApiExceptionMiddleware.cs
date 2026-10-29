using System.Text.Json;
using ERPm.Application.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ERPm.Application.Features.Localization;

namespace ERPm.API.Middlewares;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext http, ILocalizationService localization)
    {
        try { await next(http); }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested) { }
        catch (Exception error)
        {
            var (status, code) = error switch
            {
                BusinessException business => (business.StatusCode, business.Code),
                DbUpdateException { InnerException: PostgresException { SqlState: "23505" } } => (409, "error.validation.duplicate"),
                DbUpdateException { InnerException: PostgresException { SqlState: "23503" } } => (409, "error.validation.reference"),
                DbUpdateConcurrencyException => (409, "error.request.conflict"),
                _ => (500, "error.server")
            };
            if (status == 500) logger.LogError(error, "Unhandled API failure {TraceId}", http.TraceIdentifier);
            if (http.Response.HasStarted) throw;
            http.Response.StatusCode = status;
            var response = ApiResponse<object>.Fail(code);
            if (status != 500)
            {
                var language = http.Request.Headers.AcceptLanguage.ToString().StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "vi";
                var bundle = await localization.GetDictionaryBundleAsync(language, http.RequestAborted);
                if (bundle.Entries.TryGetValue(code, out var message)) response.Message = message;
            }
            await http.Response.WriteAsJsonAsync(response, new JsonSerializerOptions(JsonSerializerDefaults.Web), http.RequestAborted);
        }
    }
}
