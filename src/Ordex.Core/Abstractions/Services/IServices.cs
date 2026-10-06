using Ordex.Core.Common;
using Ordex.Core.Entities;
using Ordex.Core.Models;

namespace Ordex.Core.Abstractions.Services;

/*  Service contracts – the only thing controllers talk to.
    Services own the business rules, the tenant checks and the transactions.
    Every service works on behalf of ICurrentUser, so callers never pass a scope. */

public interface IScopeService
{
    /// <summary>True when the user must pick a company on create forms (SuperAdmin).</summary>
    bool NeedsCompany { get; }

    /// <summary>True when the user must pick a business unit on create forms.</summary>
    bool NeedsBusinessUnit { get; }

    /// <summary>Works out (and validates) which company / unit a new record belongs to.</summary>
    Task<ServiceResult<TenantScope>> ResolveForCreateAsync(int? companyId, int? businessUnitId);
}

public interface ILookupService
{
    /// <summary>Purchase / sales currency of the data currently in view (top-bar scope).</summary>
    Task<CurrencySettings> CurrencySettingsAsync();

    Task<IReadOnlyList<LookupItem>> CompaniesAsync();
    Task<IReadOnlyList<LookupItem>> BusinessUnitsAsync(int? companyId);
    Task<IReadOnlyList<BatchLookupItem>> BatchesAsync(int? companyId, int? businessUnitId);
    Task<IReadOnlyList<LookupItem>> ExpenseCategoriesAsync(int? companyId);
    /// <summary>Customer quick search (mobile or name), optionally narrowed to a company / unit inside the user's scope.</summary>
    Task<IReadOnlyList<CustomerListItem>> SearchCustomersAsync(string term, int? companyId = null, int? businessUnitId = null);
    Task<decimal> DefaultExchangeRateAsync(int? companyId);
}

public sealed class AuthenticatedUser
{
    public int Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public Enums.UserRole Role { get; init; }
    public int CompanyId { get; init; }
    public int BusinessUnitId { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string BusinessUnitName { get; init; } = string.Empty;
    public string Theme { get; init; } = Appearance.DefaultTheme;
    public string ColorMode { get; init; } = Appearance.DefaultMode;
}

public interface IAuthService
{
    Task<ServiceResult<AuthenticatedUser>> LoginAsync(LoginInput input);
    Task<ServiceResult> ChangePasswordAsync(ChangePasswordInput input);
    Task EnsureSuperAdminAsync(string userName, string password, string fullName);

    /// <summary>
    /// Re-checks a signed-in user against the database (still active, same role and scope).
    /// Lets an admin's change (deactivate / move user) take effect without waiting for the cookie to expire.
    /// </summary>
    Task<bool> IsSessionValidAsync(int userId, Enums.UserRole role, int companyId, int businessUnitId);

    /// <summary>Saves the signed-in user's theme and light/dark mode.</summary>
    Task<ServiceResult> SaveAppearanceAsync(string theme, string colorMode);
}

public interface ICompanyService
{
    Task<IReadOnlyList<Company>> GetAllAsync();
    Task<CompanyInput?> GetForEditAsync(int id);
    Task<ServiceResult<int>> SaveAsync(CompanyInput input);

    /// <summary>The signed-in Admin's own company, for the "Company profile" page.</summary>
    Task<CompanyProfileInput?> GetProfileAsync();
    Task<ServiceResult> SaveProfileAsync(CompanyProfileInput input);

    /// <summary>SuperAdmin only. Returns the company name for the message.</summary>
    Task<ServiceResult<string>> SetActiveAsync(int id, bool isActive);

    /// <summary>SuperAdmin only. Refused once the company has any data.</summary>
    Task<ServiceResult> DeleteAsync(int id);
}

public interface IBusinessUnitService
{
    Task<IReadOnlyList<BusinessUnitListItem>> GetListAsync();
    Task<BusinessUnitInput?> GetForEditAsync(int id);
    Task<ServiceResult<int>> SaveAsync(BusinessUnitInput input);
    Task<ServiceResult<string>> SetActiveAsync(int id, bool isActive);
    Task<ServiceResult> DeleteAsync(int id);
}

public interface IUserService
{
    Task<PagedResult<UserListItem>> GetPagedAsync(UserFilter filter);
    Task<UserInput?> GetForEditAsync(int id);
    Task<ServiceResult<int>> SaveAsync(UserInput input);
}

public interface ICustomerService
{
    Task<PagedResult<CustomerListItem>> GetPagedAsync(CustomerFilter filter);
    Task<Customer?> GetAsync(int id);
    Task<CustomerInput?> GetForEditAsync(int id);
    Task<ServiceResult> UpdateAsync(CustomerInput input);
}

public interface IBatchService
{
    Task<PagedResult<BatchListItem>> GetPagedAsync(BatchFilter filter);
    Task<BatchListItem?> GetTotalsAsync(int id);
    Task<BatchInput?> GetForEditAsync(int id);
    Task<BatchInput> NewAsync();
    Task<ServiceResult<int>> SaveAsync(BatchInput input);
    Task<ServiceResult> SetPaidAsync(int id, bool isPaid);
}

public interface IOrderService
{
    Task<PagedResult<OrderListItem>> GetPagedAsync(OrderFilter filter);
    Task<IReadOnlyList<OrderStatusCount>> GetStatusCountsAsync(OrderFilter filter);
    Task<IReadOnlyList<OrderListItem>> GetForExportAsync(OrderFilter filter);
    Task<OrderDetails?> GetDetailsAsync(int id);
    Task<IReadOnlyList<OrderStatusLogItem>> GetHistoryAsync(int id);
    Task<OrderInput> NewAsync();
    Task<OrderInput?> GetForEditAsync(int id);
    Task<ServiceResult<PreOrder>> SaveAsync(OrderInput input, UploadedFile? image);

    /// <summary>Blank new-order screen, pre-filled with a customer when one is given.</summary>
    Task<NewOrdersInput> NewOrdersAsync(int? customerId);

    /// <summary>One customer, several items → one pre-order per item, all or nothing. Images are matched by item index.</summary>
    Task<ServiceResult<IReadOnlyList<PreOrder>>> CreateManyAsync(NewOrdersInput input, IReadOnlyList<UploadedFile?> images);

    Task<ServiceResult<string>> DispatchAsync(OrderStatusInput input);
    Task<ServiceResult<string>> UndoDispatchAsync(OrderStatusInput input);
    Task<ServiceResult<string>> DeliverAsync(DeliverInput input);
    Task<ServiceResult<string>> ReturnAsync(ReturnInput input);
    Task<ServiceResult> DeleteAsync(int id);

    /// <summary>Makes a new tracking link for the order; the old link stops working.</summary>
    Task<ServiceResult<string>> ResetTrackingLinkAsync(int id);
}

/// <summary>Public, read-only order tracking for customers (no sign-in).</summary>
public interface ITrackingService
{
    Task<OrderTrackingResult?> GetAsync(string? token);
}

public interface IStockService
{
    Task<PagedResult<StockListItem>> GetPagedAsync(StockFilter filter);
    Task<StockTabCounts> GetTabCountsAsync();
    Task<StockInput> NewAsync();
    Task<StockInput?> GetForEditAsync(int id);
    Task<ServiceResult<int>> SaveExtraAsync(StockInput input, UploadedFile? image);
    Task<ServiceResult> SellAsync(StockSaleInput input);
    Task<ServiceResult> DeleteAsync(int id);
}

public interface IExpenseService
{
    Task<(PagedResult<ExpenseListItem> Page, decimal TotalAmount)> GetPagedAsync(ExpenseFilter filter);
    Task<ExpenseInput> NewAsync();
    Task<ExpenseInput?> GetForEditAsync(int id);
    Task<ServiceResult<int>> SaveAsync(ExpenseInput input);
    Task<ServiceResult> DeleteAsync(int id);

    Task<IReadOnlyList<ExpenseCategory>> GetCategoriesAsync();
    Task<ServiceResult<int>> SaveCategoryAsync(ExpenseCategoryInput input);
}

public interface IReportService
{
    Task<DashboardData> GetDashboardAsync(DateTime fromDate, DateTime toDate);
    Task<ProfitLossReport> GetProfitLossAsync(DateTime fromDate, DateTime toDate);
    Task<PayableReport> GetMonthlyPayableAsync(int year);
}
