using Microsoft.Extensions.Options;
using Ordex.Core.Abstractions.Services;
using Ordex.Infrastructure.Data;
using Ordex.Infrastructure.Database;

namespace Ordex.Web.Infrastructure;

public static class DatabaseInitializer
{
    /// <summary>
    /// On startup: create/upgrade the database from /database/*.sql and make sure a
    /// SuperAdmin exists. If SQL Server is unreachable the app still starts (the
    /// landing page works) and the error is logged.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Ordex.Startup");

        try
        {
            var options = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                logger.LogCritical("No database connection string. Set ConnectionStrings:Default in appsettings.json.");
                return;
            }

            if (options.AutoMigrate)
            {
                var scriptsFolder = Path.Combine(AppContext.BaseDirectory, "database");
                await services.GetRequiredService<DatabaseMigrator>().MigrateAsync(scriptsFolder);
            }

            var seed = app.Configuration.GetSection("Seed");
            await services.GetRequiredService<IAuthService>().EnsureSuperAdminAsync(
                seed["SuperAdminUserName"] ?? "superadmin",
                seed["SuperAdminPassword"] ?? "Ordex@12345",
                seed["SuperAdminFullName"] ?? "Super Admin");

            logger.LogInformation("Database is ready.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Database initialisation failed. Check the connection string and that SQL Server is running.");
        }
    }
}
