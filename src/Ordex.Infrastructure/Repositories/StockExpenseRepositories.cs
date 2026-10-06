using Ordex.Core.Abstractions.Repositories;
using Ordex.Core.Common;
using Ordex.Core.Entities;
using Ordex.Core.Enums;
using Ordex.Core.Models;
using Ordex.Infrastructure.Data;

namespace Ordex.Infrastructure.Repositories;

public sealed class StockRepository(DbSession session) : RepositoryBase(session), IStockRepository
{
    private const string EntityColumns = """
        Id, CompanyId, BusinessUnitId, SourceType, SourceOrderId, BatchId, EntryDate,
        ProductName, ProductImage, ProductLink, ProductSize, PriceSgd, PriceBdt, ReturnReason,
        Status, SoldPrice, SoldDate, SoldTo, Notes, IsActive, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt
        """;

    public Task<PagedResult<StockListItem>> GetPagedAsync(TenantScope scope, StockFilter filter)
    {
        var p = ScopeParameters(scope);
        p.Add("Search", filter.SearchPattern);

        // Tab conditions are fixed strings chosen in code – safe to inline.
        var tabWhere = filter.Tab switch
        {
            StockTab.Extra => "s.Status = 1 AND s.SourceType = 1",
            StockTab.Returned => "s.Status = 1 AND s.SourceType = 2",
            StockTab.Sold => "s.Status = 2",
            _ => "s.Status = 1"
        };

        var orderBy = filter.Tab == StockTab.Sold ? "s.SoldDate DESC, s.Id DESC" : "s.EntryDate DESC, s.Id DESC";

        return QueryPagedAsync<StockListItem>(
            """
            s.Id, s.CompanyId, s.BusinessUnitId, s.SourceType, s.SourceOrderId, o.OrderNo AS SourceOrderNo,
            s.BatchId, b.BatchNo, s.EntryDate, s.ProductName, s.ProductImage, s.ProductLink, s.ProductSize,
            s.PriceSgd, s.PriceBdt, s.ReturnReason, s.Status, s.SoldPrice, s.SoldDate, s.SoldTo, s.Notes,
            bu.UnitName AS BusinessUnitName
            """,
            $"""
            FROM dbo.StockItem s
            LEFT JOIN dbo.PreOrder o       ON o.Id  = s.SourceOrderId
            LEFT JOIN dbo.PurchaseBatch b  ON b.Id  = s.BatchId
            LEFT JOIN dbo.BusinessUnit bu  ON bu.Id = s.BusinessUnitId
            WHERE s.IsActive = 1
              AND {ScopeFilter("s")}
              AND {tabWhere}
              AND (@Search IS NULL OR s.ProductName LIKE @Search OR s.ReturnReason LIKE @Search
                   OR s.SoldTo LIKE @Search OR o.OrderNo LIKE @Search)
            """,
            orderBy, p, filter);
    }

    public async Task<StockTabCounts> GetTabCountsAsync(TenantScope scope) =>
        await QuerySingleOrDefaultAsync<StockTabCounts>($"""
            SELECT InStock      = ISNULL(SUM(CASE WHEN s.Status = 1 THEN 1 ELSE 0 END), 0),
                   Extra        = ISNULL(SUM(CASE WHEN s.Status = 1 AND s.SourceType = 1 THEN 1 ELSE 0 END), 0),
                   Returned     = ISNULL(SUM(CASE WHEN s.Status = 1 AND s.SourceType = 2 THEN 1 ELSE 0 END), 0),
                   Sold         = ISNULL(SUM(CASE WHEN s.Status = 2 THEN 1 ELSE 0 END), 0),
                   InStockValue = ISNULL(SUM(CASE WHEN s.Status = 1 THEN s.PriceBdt ELSE 0 END), 0)
            FROM dbo.StockItem s
            WHERE s.IsActive = 1 AND {ScopeFilter("s")};
            """, ScopeParameters(scope)) ?? new StockTabCounts();

    public Task<StockItem?> GetByIdAsync(int id, TenantScope scope)
    {
        var p = ScopeParameters(scope);
        p.Add("Id", id);
        return QuerySingleOrDefaultAsync<StockItem>(
            $"SELECT {EntityColumns} FROM dbo.StockItem s WHERE s.Id = @Id AND s.IsActive = 1 AND {ScopeFilter("s")};", p);
    }

    public async Task<int> InsertAsync(StockItem s) =>
        await ExecuteScalarAsync<int>("""
            INSERT INTO dbo.StockItem
                (CompanyId, BusinessUnitId, SourceType, SourceOrderId, BatchId, EntryDate, ProductName, ProductImage,
                 ProductLink, ProductSize, PriceSgd, PriceBdt, ReturnReason, Status, Notes, IsActive, CreatedBy, CreatedAt)
            VALUES
                (@CompanyId, @BusinessUnitId, @SourceType, @SourceOrderId, @BatchId, @EntryDate, @ProductName, @ProductImage,
                 @ProductLink, @ProductSize, @PriceSgd, @PriceBdt, @ReturnReason, @Status, @Notes, 1, @CreatedBy, @CreatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, s);

    public Task UpdateAsync(StockItem s) =>
        ExecuteAsync("""
            UPDATE dbo.StockItem
               SET BatchId = @BatchId, EntryDate = @EntryDate, ProductName = @ProductName, ProductImage = @ProductImage,
                   ProductLink = @ProductLink, ProductSize = @ProductSize, PriceSgd = @PriceSgd, PriceBdt = @PriceBdt,
                   Notes = @Notes, UpdatedBy = @UpdatedBy, UpdatedAt = @UpdatedAt
             WHERE Id = @Id AND IsActive = 1;
            """, s);

    public async Task<bool> UpdateSaleAsync(StockItem s, StockStatus expectedStatus) =>
        await ExecuteAsync("""
            UPDATE dbo.StockItem
               SET Status = @Status, SoldPrice = @SoldPrice, SoldDate = @SoldDate, SoldTo = @SoldTo,
                   UpdatedBy = @UpdatedBy, UpdatedAt = @UpdatedAt
             WHERE Id = @Id AND Status = @ExpectedStatus AND IsActive = 1;
            """, new { s.Id, s.Status, s.SoldPrice, s.SoldDate, s.SoldTo, s.UpdatedBy, s.UpdatedAt, ExpectedStatus = expectedStatus }) == 1;

    public Task SoftDeleteAsync(int id, int userId, DateTime at) =>
        ExecuteAsync("UPDATE dbo.StockItem SET IsActive = 0, UpdatedBy = @UserId, UpdatedAt = @At WHERE Id = @Id;",
            new { Id = id, UserId = userId, At = at });
}

public sealed class ExpenseCategoryRepository(DbSession session) : RepositoryBase(session), IExpenseCategoryRepository
{
    private const string Columns = "Id, CompanyId, BusinessUnitId, CategoryName, IsActive, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt";

    public Task<IReadOnlyList<ExpenseCategory>> GetListAsync(int companyId, bool activeOnly = true) =>
        QueryAsync<ExpenseCategory>($"""
            SELECT {Columns}
            FROM dbo.ExpenseCategory
            WHERE (@CompanyId = 0 OR CompanyId IN (0, @CompanyId))
              AND (@ActiveOnly = 0 OR IsActive = 1)
            ORDER BY CASE WHEN CompanyId = 0 THEN 0 ELSE 1 END, CategoryName;
            """, new { CompanyId = companyId, ActiveOnly = activeOnly });

    public Task<ExpenseCategory?> GetByIdAsync(int id) =>
        QuerySingleOrDefaultAsync<ExpenseCategory>($"SELECT {Columns} FROM dbo.ExpenseCategory WHERE Id = @Id;", new { Id = id });

    public async Task<int> InsertAsync(ExpenseCategory c) =>
        await ExecuteScalarAsync<int>("""
            INSERT INTO dbo.ExpenseCategory (CompanyId, BusinessUnitId, CategoryName, IsActive, CreatedBy, CreatedAt)
            VALUES (@CompanyId, @BusinessUnitId, @CategoryName, @IsActive, @CreatedBy, @CreatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, c);

    public Task UpdateAsync(ExpenseCategory c) =>
        ExecuteAsync("""
            UPDATE dbo.ExpenseCategory
               SET CategoryName = @CategoryName, IsActive = @IsActive, UpdatedBy = @UpdatedBy, UpdatedAt = @UpdatedAt
             WHERE Id = @Id;
            """, c);
}

public sealed class ExpenseRepository(DbSession session) : RepositoryBase(session), IExpenseRepository
{
    private const string Columns = """
        Id, CompanyId, BusinessUnitId, ExpenseDate, CategoryId, BatchId, Amount, Description,
        IsActive, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt
        """;

    public async Task<(PagedResult<ExpenseListItem> Page, decimal TotalAmount)> GetPagedAsync(TenantScope scope, ExpenseFilter filter)
    {
        var p = ScopeParameters(scope);
        p.Add("Search", filter.SearchPattern);
        p.Add("FromDate", filter.FromDate?.Date);
        p.Add("ToDate", filter.ToDate?.Date);
        p.Add("CategoryId", filter.CategoryId);

        var fromWhere = $"""
            FROM dbo.Expense e
            INNER JOIN dbo.ExpenseCategory c ON c.Id = e.CategoryId
            LEFT  JOIN dbo.PurchaseBatch b   ON b.Id = e.BatchId
            LEFT  JOIN dbo.BusinessUnit bu   ON bu.Id = e.BusinessUnitId
            WHERE e.IsActive = 1
              AND {ScopeFilter("e")}
              AND (@FromDate IS NULL OR e.ExpenseDate >= @FromDate)
              AND (@ToDate IS NULL OR e.ExpenseDate <= @ToDate)
              AND (@CategoryId IS NULL OR e.CategoryId = @CategoryId)
              AND (@Search IS NULL OR e.Description LIKE @Search OR c.CategoryName LIKE @Search)
            """;

        var page = await QueryPagedAsync<ExpenseListItem>(
            """
            e.Id, e.ExpenseDate, e.CategoryId, c.CategoryName, e.Amount, e.Description,
            b.BatchNo, bu.UnitName AS BusinessUnitName
            """,
            fromWhere, "e.ExpenseDate DESC, e.Id DESC", p, filter);

        var total = await ExecuteScalarAsync<decimal>($"SELECT ISNULL(SUM(e.Amount), 0) {fromWhere};", p);
        return (page, total);
    }

    public Task<Expense?> GetByIdAsync(int id, TenantScope scope)
    {
        var p = ScopeParameters(scope);
        p.Add("Id", id);
        return QuerySingleOrDefaultAsync<Expense>(
            $"SELECT {Columns} FROM dbo.Expense e WHERE e.Id = @Id AND e.IsActive = 1 AND {ScopeFilter("e")};", p);
    }

    public async Task<int> InsertAsync(Expense e) =>
        await ExecuteScalarAsync<int>("""
            INSERT INTO dbo.Expense (CompanyId, BusinessUnitId, ExpenseDate, CategoryId, BatchId, Amount, Description, IsActive, CreatedBy, CreatedAt)
            VALUES (@CompanyId, @BusinessUnitId, @ExpenseDate, @CategoryId, @BatchId, @Amount, @Description, 1, @CreatedBy, @CreatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, e);

    public Task UpdateAsync(Expense e) =>
        ExecuteAsync("""
            UPDATE dbo.Expense
               SET ExpenseDate = @ExpenseDate, CategoryId = @CategoryId, BatchId = @BatchId, Amount = @Amount,
                   Description = @Description, UpdatedBy = @UpdatedBy, UpdatedAt = @UpdatedAt
             WHERE Id = @Id AND IsActive = 1;
            """, e);

    public Task SoftDeleteAsync(int id, int userId, DateTime at) =>
        ExecuteAsync("UPDATE dbo.Expense SET IsActive = 0, UpdatedBy = @UserId, UpdatedAt = @At WHERE Id = @Id;",
            new { Id = id, UserId = userId, At = at });
}
