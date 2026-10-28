using ERPm.API.Middlewares;
using ERPm.Infrastructure;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Persistence.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Infrastructure (EF Core, Services, Repositories)
builder.Services.AddInfrastructure(builder.Configuration);

// 2. Add API Controllers & JSON Options
builder.Services.AddControllers();

// 3. Add CORS for Frontend (Angular / Ionic Web & Capacitor)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 4. Swagger / OpenAPI Configuration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ERPm API - Sales, Inventory & Accounting System",
        Version = "v1",
        Description = "Core Backend RESTful API for ERPm (PostgreSQL, .NET 8, Clean Architecture, i18n & Double-Entry ready)"
    });
});

var app = builder.Build();

// 5. Global Exception Handling Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 6. Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ERPm API v1");
        c.RoutePrefix = string.Empty; // Swagger at root http://localhost:5000/
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// 7. Auto-Migration & Data Seeding (Safe check)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        // Only attempt DB operation if database can connect
        if (context.Database.CanConnect())
        {
            logger.LogInformation("Database connected. Applying migrations...");
            await context.Database.MigrateAsync();
            // await DataSeeder.SeedAsync(context); // Tạm thời không seed data theo yêu cầu
            logger.LogInformation("Database migration check completed successfully.");
        }
        else
        {
            logger.LogWarning("PostgreSQL database is currently not accessible. Please start PostgreSQL container via docker-compose up -d.");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not connect to PostgreSQL database on startup. Ensure Docker container or local PostgreSQL service is running.");
    }
}

app.Run();
