using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ordex.Core.Abstractions;
using Ordex.Core.Abstractions.Repositories;
using Ordex.Infrastructure.Data;
using Ordex.Infrastructure.Database;
using Ordex.Infrastructure.Files;
using Ordex.Infrastructure.Repositories;
using Ordex.Infrastructure.Security;

namespace Ordex.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.PostConfigure<DatabaseOptions>(o =>
        {
            // Allow the standard ConnectionStrings:Default as well.
            if (string.IsNullOrWhiteSpace(o.ConnectionString))
                o.ConnectionString = configuration.GetConnectionString("Default") ?? string.Empty;
        });
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.Configure<AppClockOptions>(configuration.GetSection(AppClockOptions.SectionName));

        // Data access – one session (connection + transaction) per request
        services.AddScoped<DbSession>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IBusinessUnitRepository, BusinessUnitRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IBatchRepository, BatchRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IExpenseCategoryRepository, ExpenseCategoryRepository>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IDocumentNumberRepository, DocumentNumberRepository>();

        // Cross-cutting
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddTransient<DatabaseMigrator>();

        return services;
    }
}
