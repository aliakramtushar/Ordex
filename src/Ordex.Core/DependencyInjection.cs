using Microsoft.Extensions.DependencyInjection;
using Ordex.Core.Abstractions.Services;
using Ordex.Core.Services;

namespace Ordex.Core;

public static class DependencyInjection
{
    /// <summary>Registers all business services (one instance per HTTP request).</summary>
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddScoped<IScopeService, ScopeService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IBusinessUnitService, BusinessUnitService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IBatchService, BatchService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ITrackingService, TrackingService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IReportService, ReportService>();
        return services;
    }
}
