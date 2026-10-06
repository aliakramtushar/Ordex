using Ordex.Core.Common;
using Ordex.Core.Entities;
using Ordex.Core.Enums;
using Ordex.Core.Models;

namespace Ordex.Core.Abstractions.Repositories;

/*  Repository contracts.
    Rule: every read of tenant data takes a TenantScope, so a user can never
    load a record outside his company / business unit (even by guessing an Id). */

public interface ICompanyRepository
{
    Task<IReadOnlyList<Company>> GetAllAsync(bool activeOnly = false);
    Task<Company?> GetByIdAsync(int id);
    Task<bool> CodeExistsAsync(string code, int excludeId = 0);
    Task<int> InsertAsync(Company company);
    Task UpdateAsync(Company company);
    Task SetActiveAsync(int id, bool isActive, int updatedBy, DateTime updatedAt);

    /// <summary>True when users, customers, batches, orders, stock, expenses or own categories exist for the company.</summary>
    Task<bool> HasDataAsync(int id);

    /// <summary>Deletes the company with its (empty) business units and number sequences. Run inside a transaction.</summary>
    Task DeleteAsync(int id);
}

public interface IBusinessUnitRepository
{
    /// <param name="companyId">0 = all companies.</param>
    Task<IReadOnlyList<BusinessUnitListItem>> GetListAsync(int companyId, bool activeOnly = false);
    Task<BusinessUnit?> GetByIdAsync(int id);
    Task<bool> CodeExistsAsync(int companyId, string code, int excludeId = 0);
    Task<int> InsertAsync(BusinessUnit unit);
    Task UpdateAsync(BusinessUnit unit);
    Task SetActiveAsync(int id, bool isActive, int updatedBy, DateTime updatedAt);

    /// <summary>True when users, customers, batches, orders, stock, expenses or own categories exist for the unit.</summary>
    Task<bool> HasDataAsync(int id);
    Task DeleteAsync(int id);
}

public interface IUserRepository
{
    Task<AppUser?> GetByUserNameAsync(string userName);
    Task<AppUser?> GetByIdAsync(int id);
    Task<PagedResult<UserListItem>> GetPagedAsync(TenantScope scope, UserFilter filter);
    Task<bool> UserNameExistsAsync(string userName, int excludeId = 0);
    Task<bool> AnySuperAdminAsync();
    Task<int> InsertAsync(AppUser user);
    Task UpdateAsync(AppUser user);
    Task UpdatePasswordAsync(int userId, string passwordHash, int updatedBy, DateTime updatedAt);
    Task RecordLoginSuccessAsync(int userId, DateTime loginAt);
    Task UpdateAppearanceAsync(int userId, string theme, string colorMode);
    Task RecordLoginFailureAsync(int userId, int failedCount, DateTime? lockoutEnd);
}

public interface ICustomerRepository
{
    Task<PagedResult<CustomerListItem>> GetPagedAsync(TenantScope scope, CustomerFilter filter);
    Task<Customer?> GetByIdAsync(int id, TenantScope scope);
    Task<Customer?> FindByMobileAsync(int companyId, int businessUnitId, string mobile);
    Task<IReadOnlyList<CustomerListItem>> SearchAsync(TenantScope scope, string term, int take = 8);
    Task<int> InsertAsync(Customer customer);
    Task UpdateAsync(Customer customer);
}

public interface IBatchRepository
{
    Task<PagedResult<BatchListItem>> GetPagedAsync(TenantScope scope, BatchFilter filter);
    Task<PurchaseBatch?> GetByIdAsync(int id, TenantScope scope);
    Task<BatchListItem?> GetTotalsAsync(int id, TenantScope scope);
    Task<IReadOnlyList<BatchLookupItem>> GetLookupAsync(int companyId, int businessUnitId);
    Task<bool> BatchNoExistsAsync(int companyId, int businessUnitId, string batchNo, int excludeId = 0);
    Task<int> InsertAsync(PurchaseBatch batch);
    Task UpdateAsync(PurchaseBatch batch);
    Task SetPaidAsync(int id, bool isPaid, DateTime? paidDate, int userId, DateTime at);
}

public interface IOrderRepository
{
    Task<PagedResult<OrderListItem>> GetPagedAsync(TenantScope scope, OrderFilter filter);
    Task<IReadOnlyList<OrderStatusCount>> GetStatusCountsAsync(TenantScope scope, OrderFilter filter);
    Task<PreOrder?> GetByIdAsync(int id, TenantScope scope);
    Task<OrderDetails?> GetDetailsAsync(int id, TenantScope scope);
    Task<IReadOnlyList<OrderStatusLogItem>> GetStatusLogAsync(int orderId);
    Task<int> InsertAsync(PreOrder order);
    Task UpdateAsync(PreOrder order);

    /// <summary>
    /// Saves the status columns only if the row still has <paramref name="expectedStatus"/>.
    /// Returns false when someone else changed it first (double tap / two devices).
    /// </summary>
    Task<bool> UpdateStatusAsync(PreOrder order, OrderStatus expectedStatus);
    Task InsertStatusLogAsync(OrderStatusLog log);

    /// <summary>Public tracking page: the order behind a link (any tenant – the token is the key).</summary>
    Task<OrderTrackingView?> GetByTrackingTokenAsync(string token);

    /// <summary>The customer's other open orders (pre-order / out for delivery), newest first.</summary>
    Task<IReadOnlyList<OrderTrackingSummary>> GetOpenTrackingByCustomerAsync(int customerId, int excludeOrderId, int take = 10);

    Task SetTrackingTokenAsync(int orderId, string token);
    Task SoftDeleteAsync(int id, int userId, DateTime at);
}

public interface IStockRepository
{
    Task<PagedResult<StockListItem>> GetPagedAsync(TenantScope scope, StockFilter filter);
    Task<StockTabCounts> GetTabCountsAsync(TenantScope scope);
    Task<StockItem?> GetByIdAsync(int id, TenantScope scope);
    Task<int> InsertAsync(StockItem item);
    Task UpdateAsync(StockItem item);
    Task<bool> UpdateSaleAsync(StockItem item, StockStatus expectedStatus);
    Task SoftDeleteAsync(int id, int userId, DateTime at);
}

public interface IExpenseCategoryRepository
{
    /// <summary>Shared categories (CompanyId = 0) plus the company's own.</summary>
    Task<IReadOnlyList<ExpenseCategory>> GetListAsync(int companyId, bool activeOnly = true);
    Task<ExpenseCategory?> GetByIdAsync(int id);
    Task<int> InsertAsync(ExpenseCategory category);
    Task UpdateAsync(ExpenseCategory category);
}

public interface IExpenseRepository
{
    Task<(PagedResult<ExpenseListItem> Page, decimal TotalAmount)> GetPagedAsync(TenantScope scope, ExpenseFilter filter);
    Task<Expense?> GetByIdAsync(int id, TenantScope scope);
    Task<int> InsertAsync(Expense expense);
    Task UpdateAsync(Expense expense);
    Task SoftDeleteAsync(int id, int userId, DateTime at);
}

/// <summary>Reporting – backed by stored procedures.</summary>
public interface IReportRepository
{
    Task<DashboardData> GetDashboardAsync(TenantScope scope, DateTime fromDate, DateTime toDate);
    Task<ProfitLossReport> GetProfitLossAsync(TenantScope scope, DateTime fromDate, DateTime toDate);
    Task<PayableReport> GetMonthlyPayableAsync(TenantScope scope, int year);
}

public interface IDocumentNumberRepository
{
    /// <summary>Next number like "ORD-2610-0001". Must run inside a transaction.</summary>
    Task<string> NextAsync(int companyId, string prefix, DateTime date);
}
