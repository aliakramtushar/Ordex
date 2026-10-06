using Ordex.Core.Abstractions.Repositories;
using Ordex.Core.Common;
using Ordex.Core.Entities;
using Ordex.Core.Models;
using Ordex.Infrastructure.Data;

namespace Ordex.Infrastructure.Repositories;

public sealed class CustomerRepository(DbSession session) : RepositoryBase(session), ICustomerRepository
{
    private const string Columns = """
        Id, CompanyId, BusinessUnitId, CustomerName, Mobile, SocialLink, Address,
        IsActive, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt
        """;

    public Task<PagedResult<CustomerListItem>> GetPagedAsync(TenantScope scope, CustomerFilter filter)
    {
        var p = ScopeParameters(scope);
        p.Add("Search", filter.SearchPattern);

        return QueryPagedAsync<CustomerListItem>(
            """
            c.Id, c.CustomerName, c.Mobile, c.SocialLink, c.Address, bu.UnitName AS BusinessUnitName,
            ISNULL(s.OrderCount, 0) AS OrderCount, ISNULL(s.TotalSpent, 0) AS TotalSpent
            """,
            $"""
            FROM dbo.Customer c
            LEFT JOIN dbo.BusinessUnit bu ON bu.Id = c.BusinessUnitId
            OUTER APPLY (
                SELECT OrderCount = COUNT(1),
                       TotalSpent = SUM(CASE WHEN o.Status = 3 THEN o.SellingPrice ELSE 0 END)
                FROM dbo.PreOrder o
                WHERE o.CustomerId = c.Id AND o.IsActive = 1
            ) s
            WHERE c.IsActive = 1
              AND {ScopeFilter("c")}
              AND (@Search IS NULL OR c.CustomerName LIKE @Search OR c.Mobile LIKE @Search)
            """,
            "c.CustomerName, c.Id",
            p, filter);
    }

    public Task<Customer?> GetByIdAsync(int id, TenantScope scope)
    {
        var p = ScopeParameters(scope);
        p.Add("Id", id);
        return QuerySingleOrDefaultAsync<Customer>(
            $"SELECT {Columns} FROM dbo.Customer c WHERE c.Id = @Id AND c.IsActive = 1 AND {ScopeFilter("c")};", p);
    }

    public Task<Customer?> FindByMobileAsync(int companyId, int businessUnitId, string mobile) =>
        QuerySingleOrDefaultAsync<Customer>($"""
            SELECT TOP (1) {Columns}
            FROM dbo.Customer
            WHERE CompanyId = @CompanyId AND BusinessUnitId = @BusinessUnitId AND Mobile = @Mobile AND IsActive = 1
            ORDER BY Id;
            """, new { CompanyId = companyId, BusinessUnitId = businessUnitId, Mobile = mobile });

    /// <summary>
    /// Quick search for the order screen: mobile or name, best matches first
    /// (mobile starts-with, then name starts-with, then most recent buyers).
    /// </summary>
    public Task<IReadOnlyList<CustomerListItem>> SearchAsync(TenantScope scope, string term, int take = 8)
    {
        var p = ScopeParameters(scope);
        var digits = TextHelper.NormalizeMobile(term);
        p.Add("Term", $"%{term}%");
        p.Add("Starts", $"{term}%");
        p.Add("Digits", digits.Length >= 3 ? $"%{digits}%" : null);
        p.Add("Take", take);
        return QueryAsync<CustomerListItem>($"""
            SELECT TOP (@Take)
                   c.Id, c.CompanyId, c.BusinessUnitId, c.CustomerName, c.Mobile, c.SocialLink, c.Address,
                   bu.UnitName AS BusinessUnitName,
                   ISNULL(s.OrderCount, 0) AS OrderCount, ISNULL(s.TotalSpent, 0) AS TotalSpent, s.LastOrderDate
            FROM dbo.Customer c
            LEFT JOIN dbo.BusinessUnit bu ON bu.Id = c.BusinessUnitId
            OUTER APPLY (
                SELECT OrderCount    = COUNT(1),
                       TotalSpent    = SUM(CASE WHEN o.Status = 3 THEN o.SellingPrice ELSE 0 END),
                       LastOrderDate = MAX(o.OrderDate)
                FROM dbo.PreOrder o
                WHERE o.CustomerId = c.Id AND o.IsActive = 1
            ) s
            WHERE c.IsActive = 1 AND {ScopeFilter("c")}
              AND (c.CustomerName LIKE @Term OR c.Mobile LIKE @Term OR (@Digits IS NOT NULL AND c.Mobile LIKE @Digits))
            ORDER BY CASE WHEN c.Mobile LIKE @Starts THEN 0 WHEN c.CustomerName LIKE @Starts THEN 1 ELSE 2 END,
                     s.LastOrderDate DESC, c.CustomerName;
            """, p);
    }

    public async Task<int> InsertAsync(Customer c) =>
        await ExecuteScalarAsync<int>("""
            INSERT INTO dbo.Customer (CompanyId, BusinessUnitId, CustomerName, Mobile, SocialLink, Address, IsActive, CreatedBy, CreatedAt)
            VALUES (@CompanyId, @BusinessUnitId, @CustomerName, @Mobile, @SocialLink, @Address, 1, @CreatedBy, @CreatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, c);

    public Task UpdateAsync(Customer c) =>
        ExecuteAsync("""
            UPDATE dbo.Customer
               SET CustomerName = @CustomerName, Mobile = @Mobile, SocialLink = @SocialLink, Address = @Address,
                   UpdatedBy = @UpdatedBy, UpdatedAt = @UpdatedAt
             WHERE Id = @Id;
            """, c);
}

public sealed class BatchRepository(DbSession session) : RepositoryBase(session), IBatchRepository
{
    private const string Columns = """
        Id, CompanyId, BusinessUnitId, BatchNo, BatchDate, ExchangeRate, IsPaid, PaidDate, Notes,
        IsActive, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt
        """;

    private const string TotalsColumns = """
        b.BatchId, b.CompanyId, b.BusinessUnitId, b.BatchNo, b.BatchDate, b.ExchangeRate, b.IsPaid, b.PaidDate, b.Notes,
        b.OrderCount, b.ExtraCount, b.OrderSgd, b.ExtraSgd, b.TotalSgd, b.TotalBdt, b.PayableBdt,
        bu.UnitName AS BusinessUnitName
        """;

    public Task<PagedResult<BatchListItem>> GetPagedAsync(TenantScope scope, BatchFilter filter)
    {
        var p = ScopeParameters(scope);
        p.Add("Search", filter.SearchPattern);
        p.Add("IsPaid", filter.IsPaid);

        return QueryPagedAsync<BatchListItem>(
            TotalsColumns,
            $"""
            FROM dbo.vw_BatchTotals b
            LEFT JOIN dbo.BusinessUnit bu ON bu.Id = b.BusinessUnitId
            WHERE b.IsActive = 1
              AND {ScopeFilter("b")}
              AND (@IsPaid IS NULL OR b.IsPaid = @IsPaid)
              AND (@Search IS NULL OR b.BatchNo LIKE @Search OR b.Notes LIKE @Search)
            """,
            "b.BatchDate DESC, b.BatchId DESC",
            p, filter);
    }

    public Task<PurchaseBatch?> GetByIdAsync(int id, TenantScope scope)
    {
        var p = ScopeParameters(scope);
        p.Add("Id", id);
        return QuerySingleOrDefaultAsync<PurchaseBatch>(
            $"SELECT {Columns} FROM dbo.PurchaseBatch b WHERE b.Id = @Id AND b.IsActive = 1 AND {ScopeFilter("b")};", p);
    }

    public Task<BatchListItem?> GetTotalsAsync(int id, TenantScope scope)
    {
        var p = ScopeParameters(scope);
        p.Add("Id", id);
        return QuerySingleOrDefaultAsync<BatchListItem>($"""
            SELECT {TotalsColumns}
            FROM dbo.vw_BatchTotals b
            LEFT JOIN dbo.BusinessUnit bu ON bu.Id = b.BusinessUnitId
            WHERE b.BatchId = @Id AND b.IsActive = 1 AND {ScopeFilter("b")};
            """, p);
    }

    public Task<IReadOnlyList<BatchLookupItem>> GetLookupAsync(int companyId, int businessUnitId) =>
        QueryAsync<BatchLookupItem>($"""
            SELECT TOP (100) b.Id, b.BatchNo, b.BatchDate, b.ExchangeRate, b.IsPaid
            FROM dbo.PurchaseBatch b
            WHERE b.IsActive = 1 AND {ScopeFilter("b")}
            ORDER BY b.BatchDate DESC, b.Id DESC;
            """, new { CompanyId = companyId, BusinessUnitId = businessUnitId });

    public async Task<bool> BatchNoExistsAsync(int companyId, int businessUnitId, string batchNo, int excludeId = 0) =>
        await ExecuteScalarAsync<int>("""
            SELECT COUNT(1) FROM dbo.PurchaseBatch
            WHERE CompanyId = @CompanyId AND BusinessUnitId = @BusinessUnitId AND BatchNo = @BatchNo
              AND IsActive = 1 AND Id <> @ExcludeId;
            """, new { CompanyId = companyId, BusinessUnitId = businessUnitId, BatchNo = batchNo, ExcludeId = excludeId }) > 0;

    public async Task<int> InsertAsync(PurchaseBatch b) =>
        await ExecuteScalarAsync<int>("""
            INSERT INTO dbo.PurchaseBatch (CompanyId, BusinessUnitId, BatchNo, BatchDate, ExchangeRate, IsPaid, Notes, IsActive, CreatedBy, CreatedAt)
            VALUES (@CompanyId, @BusinessUnitId, @BatchNo, @BatchDate, @ExchangeRate, 0, @Notes, 1, @CreatedBy, @CreatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, b);

    public Task UpdateAsync(PurchaseBatch b) =>
        ExecuteAsync("""
            UPDATE dbo.PurchaseBatch
               SET BatchNo = @BatchNo, BatchDate = @BatchDate, ExchangeRate = @ExchangeRate, Notes = @Notes,
                   UpdatedBy = @UpdatedBy, UpdatedAt = @UpdatedAt
             WHERE Id = @Id;
            """, b);

    public Task SetPaidAsync(int id, bool isPaid, DateTime? paidDate, int userId, DateTime at) =>
        ExecuteAsync("""
            UPDATE dbo.PurchaseBatch
               SET IsPaid = @IsPaid, PaidDate = @PaidDate, UpdatedBy = @UserId, UpdatedAt = @At
             WHERE Id = @Id;
            """, new { Id = id, IsPaid = isPaid, PaidDate = paidDate, UserId = userId, At = at });
}
