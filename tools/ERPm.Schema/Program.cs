using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql("Host=localhost;Database=design;Username=design").UseSnakeCaseNamingConvention().Options);
if (args.Length != 1) throw new ArgumentException("Pass the output SQL path.");
File.WriteAllText(args[0], context.Database.GenerateCreateScript());
Console.WriteLine("Current schema generated without connecting to PostgreSQL.");
