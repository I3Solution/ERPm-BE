using System.Threading.RateLimiting;
using ERPm.API.Middlewares;
using ERPm.API.Security;
using ERPm.Application.Common.Interfaces;
using ERPm.Infrastructure;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Persistence.Seeders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args.Where(a => a != "--migrate").ToArray());
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true).AddEnvironmentVariables().AddCommandLine(args.Where(a => a != "--migrate").ToArray());
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<PermissionFilter>();
builder.Services.AddControllers(options => options.Filters.AddService<PermissionFilter>()).ConfigureApiBehaviorOptions(options =>
    options.InvalidModelStateResponseFactory = _ => new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(ERPm.Application.Common.ApiResponse<object>.Fail("error.validation.request")));
builder.Services.AddAuthentication("ERPmBearer").AddBearerToken("ERPmBearer", options =>
{
    options.BearerTokenExpiration = TimeSpan.FromHours(1);
    options.RefreshTokenExpiration = TimeSpan.FromHours(1);
});
var protection = builder.Services.AddDataProtection().SetApplicationName("ERPm").PersistKeysToFileSystem(new DirectoryInfo(
    builder.Configuration["DataProtection:KeyPath"] ?? Path.Combine(builder.Environment.ContentRootPath, ".keys")));
var certificatePath = builder.Configuration["DataProtection:CertificatePath"];
if (!string.IsNullOrWhiteSpace(certificatePath)) protection.ProtectKeysWithCertificate(
    X509CertificateLoader.LoadPkcs12FromFile(certificatePath, builder.Configuration["DataProtection:CertificatePassword"]));
else if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("Configure DataProtection:CertificatePath for production token key encryption.");
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.AddPolicy("Write", policy => policy.RequireAuthenticatedUser().RequireRole("Admin", "Operator"));
    options.AddPolicy("Admin", policy => policy.RequireAuthenticatedUser().RequireRole("Admin"));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    else policy.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo { Title = "ERPm API", Version = "v1" }));
var app = builder.Build();
app.UseMiddleware<ExceptionHandlingMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.RoutePrefix = "swagger");
}
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    var accessToken = context.Request.Query["access_token"];
    var path = context.Request.Path;
    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
    {
        context.Request.Headers.Authorization = $"Bearer {accessToken}";
    }
    await next();
});
app.UseAuthentication();
app.UseMiddleware<AccountValidationMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ERPm.Infrastructure.Hubs.NotificationHub>("/hubs/notifications");
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (args.Contains("--migrate") || builder.Configuration.GetValue<bool>("Database:AutoMigrate"))
    {
        await db.Database.MigrateAsync();
        await EssentialDataSeeder.SeedAsync(db);
    }
    else
    {
        if (!await db.Database.CanConnectAsync()) throw new InvalidOperationException("Database unavailable. Configure ConnectionStrings:DefaultConnection and run with --migrate first.");
        if ((await db.Database.GetPendingMigrationsAsync()).Any()) throw new InvalidOperationException("Database migrations pending. Run with --migrate before starting the API.");
    }
    var password = builder.Configuration["BootstrapAdmin:Password"];
    if (!string.IsNullOrEmpty(password) && !await db.AppUsers.AnyAsync(u => u.UserName == "admin"))
        await scope.ServiceProvider.GetRequiredService<AccountService>().CreateAsync(new(
            builder.Configuration["BootstrapAdmin:UserName"] ?? "admin", "Administrator", password, "Admin", []), default);
}
if (!args.Contains("--migrate")) await app.RunAsync();
