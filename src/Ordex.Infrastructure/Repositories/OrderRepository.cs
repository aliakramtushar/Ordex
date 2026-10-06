using Dapper;
using Ordex.Core.Abstractions.Repositories;
using Ordex.Core.Common;
using Ordex.Core.Entities;
using Ordex.Core.Enums;
using Ordex.Core.Models;
using Ordex.Infrastructure.Data;

namespace Ordex.Infrastructure.Repositories;

public sealed class OrderRepository(DbSession session) : RepositoryBase(session), IOrderRepository
{
    private const string EntityColumns = """
        Id, CompanyId, BusinessUnitId, OrderNo, TrackingToken, OrderDate, CustomerId, BatchId, DeliveryAddress,
        ProductName, ProductImage, ProductLink, ProductSize,
        PurchasePriceSgd, ProductPriceBdt, SellingPrice, AdvanceAmount, DueAmount, Profit,
        Status, DispatchedAt, DeliveredAt, CollectedAmount, ReturnedAt, ReturnReason, RefundAmount, Notes,
        IsActive, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt
        """;

    private const string ListColumns = """
        o.Id, o.CompanyId, o.BusinessUnitId, o.OrderNo, o.OrderDate,
        o.CustomerId, c.CustomerName, c.Mobile, c.SocialLink, o.DeliveryAddress,
        o.ProductName, o.ProductImage, o.ProductSize,
        o.PurchasePriceSgd, o.ProductPriceBdt, o.SellingPrice, o.AdvanceAmount, o.DueAmount, o.Profit, o.CollectedAmount,
        o.Status, o.DeliveredAt, o.ReturnReason, o.BatchId, b.BatchNo, bu.UnitName AS BusinessUnitName, o.TrackingToken
        """;

    private const string ListFrom = """
        FROM dbo.PreOrder o
        INNER JOIN dbo.Customer c      ON c.Id  = o.CustomerId
        LEFT  JOIN dbo.PurchaseBatch b ON b.Id  = o.BatchId
        LEFT  JOIN dbo.BusinessUnit bu ON bu.Id = o.BusinessUnitId
        """;

    public Task<PagedResult<OrderListItem>> GetPagedAsync(TenantScope scope, OrderFilter filter)
    {
        var p = FilterParameters(scope, filter);
        return QueryPagedAsync<OrderListItem>(
            ListColumns,
            $"{ListFrom} WHERE {FilterWhere(includeStatus: true)}",
            "o.OrderDate DESC, o.Id DESC",
            p, filter);
    }

    public Task<IReadOnlyList<OrderStatusCount>> GetStatusCountsAsync(TenantScope scope, OrderFilter filter) =>
        QueryAsync<OrderStatusCount>($"""
            SELECT o.Status, COUNT(1) AS Total
            {ListFrom}
            WHERE {FilterWhere(includeStatus: false)}
            GROUP BY o.Status;
            """, FilterParameters(scope, filter));

    public Task<PreOrder?> GetByIdAsync(int id, TenantScope scope)
    {
        var p = ScopeParameters(scope);
        p.Add("Id", id);
        return QuerySingleOrDefaultAsync<PreOrder>($"""
            SELECT {EntityColumns}
            FROM dbo.PreOrder o
            WHERE o.Id = @Id AND o.IsActive = 1 AND {ScopeFilter("o")};
            """, p);
    }

    public Task<OrderDetails?> GetDetailsAsync(int id, TenantScope scope)
    {
        var p = ScopeParameters(scope);
        p.Add("Id", id);
        return QuerySingleOrDefaultAsync<OrderDetails>($"""
            SELECT {ListColumns},
                   o.ProductLink, o.Notes, b.ExchangeRate, o.DispatchedAt, o.ReturnedAt, o.RefundAmount,
                   co.CompanyName, u.FullName AS CreatedByName, o.CreatedAt
            {ListFrom}
            LEFT JOIN dbo.Company co ON co.Id = o.CompanyId
            LEFT JOIN dbo.AppUser u  ON u.Id  = o.CreatedBy
            WHERE o.Id = @Id AND o.IsActive = 1 AND {ScopeFilter("o")};
            """, p);
    }

    public Task<IReadOnlyList<OrderStatusLogItem>> GetStatusLogAsync(int orderId) =>
        QueryAsync<OrderStatusLogItem>("""
            SELECT l.FromStatus, l.ToStatus, l.Remarks, u.FullName AS ChangedByName, l.ChangedAt
            FROM dbo.OrderStatusLog l
            LEFT JOIN dbo.AppUser u ON u.Id = l.ChangedBy
            WHERE l.OrderId = @OrderId
            ORDER BY l.Id DESC;
            """, new { OrderId = orderId });

    public async Task<int> InsertAsync(PreOrder o) =>
        await ExecuteScalarAsync<int>("""
            INSERT INTO dbo.PreOrder
                (CompanyId, BusinessUnitId, OrderNo, TrackingToken, OrderDate, CustomerId, BatchId, DeliveryAddress,
                 ProductName, ProductImage, ProductLink, ProductSize,
                 PurchasePriceSgd, ProductPriceBdt, SellingPrice, AdvanceAmount,
                 Status, Notes, IsActive, CreatedBy, CreatedAt)
            VALUES
                (@CompanyId, @BusinessUnitId, @OrderNo, @TrackingToken, @OrderDate, @CustomerId, @BatchId, @DeliveryAddress,
                 @ProductName, @ProductImage, @ProductLink, @ProductSize,
                 @PurchasePriceSgd, @ProductPriceBdt, @SellingPrice, @AdvanceAmount,
                 @Status, @Notes, 1, @CreatedBy, @CreatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, o);

    public Task UpdateAsync(PreOrder o) =>
        ExecuteAsync("""
            UPDATE dbo.PreOrder
               SET OrderDate = @OrderDate, CustomerId = @CustomerId, BatchId = @BatchId, DeliveryAddress = @DeliveryAddress,
                   ProductName = @ProductName, ProductImage = @ProductImage, ProductLink = @ProductLink, ProductSize = @ProductSize,
                   PurchasePriceSgd = @PurchasePriceSgd, ProductPriceBdt = @ProductPriceBdt,
                   SellingPrice = @SellingPrice, AdvanceAmount = @AdvanceAmount, Notes = @Notes,
                   UpdatedBy = @UpdatedBy, UpdatedAt = @UpdatedAt
             WHERE Id = @Id AND IsActive = 1;
            """, o);

    public async Task<bool> UpdateStatusAsync(PreOrder o, OrderStatus expectedStatus)
    {
        var rows = await ExecuteAsync("""
            UPDATE dbo.PreOrder
               SET Status = @Status, DispatchedAt = @DispatchedAt, DeliveredAt = @DeliveredAt,
                   CollectedAmount = @CollectedAmount, ReturnedAt = @ReturnedAt, ReturnReason = @ReturnReason,
                   RefundAmount = @RefundAmount, UpdatedBy = @UpdatedBy, UpdatedAt = @UpdatedAt
             WHERE Id = @Id AND Status = @ExpectedStatus AND IsActive = 1;
            """, new
        {
            o.Id,
            o.Status,
            o.DispatchedAt,
            o.DeliveredAt,
            o.CollectedAmount,
            o.ReturnedAt,
            o.ReturnReason,
            o.RefundAmount,
            o.UpdatedBy,
            o.UpdatedAt,
            ExpectedStatus = expectedStatus
        });

        return rows == 1;
    }

    public Task InsertStatusLogAsync(OrderStatusLog log) =>
        ExecuteAsync("""
            INSERT INTO dbo.OrderStatusLog (CompanyId, BusinessUnitId, OrderId, FromStatus, ToStatus, Remarks, ChangedBy, ChangedAt)
            VALUES (@CompanyId, @BusinessUnitId, @OrderId, @FromStatus, @ToStatus, @Remarks, @ChangedBy, @ChangedAt);
            """, log);

    public Task<OrderTrackingView?> GetByTrackingTokenAsync(string token) =>
        QuerySingleOrDefaultAsync<OrderTrackingView>("""
            SELECT o.Id, o.CompanyId, o.BusinessUnitId, o.CustomerId, o.OrderNo, o.OrderDate,
                   c.CustomerName, c.Mobile,
                   o.ProductName, o.ProductImage, o.ProductSize,
                   o.SellingPrice, o.AdvanceAmount, o.CollectedAmount, o.RefundAmount,
                   o.Status, o.CreatedAt, o.DispatchedAt, o.DeliveredAt, o.ReturnedAt,
                   co.CompanyName, co.Phone AS CompanyPhone, bu.UnitName AS BusinessUnitName,
                   co.PurchaseCurrency, co.SalesCurrency, o.TrackingToken
            FROM dbo.PreOrder o
            INNER JOIN dbo.Customer c      ON c.Id  = o.CustomerId
            INNER JOIN dbo.Company co      ON co.Id = o.CompanyId
            LEFT  JOIN dbo.BusinessUnit bu ON bu.Id = o.BusinessUnitId
            WHERE o.TrackingToken = @Token AND o.IsActive = 1 AND co.IsActive = 1;
            """, new { Token = token });

    public Task<IReadOnlyList<OrderTrackingSummary>> GetOpenTrackingByCustomerAsync(int customerId, int excludeOrderId, int take = 10) =>
        QueryAsync<OrderTrackingSummary>("""
            SELECT TOP (@Take) o.OrderNo, o.OrderDate, o.ProductName, o.ProductSize, o.ProductImage,
                   o.Status, o.SellingPrice, o.AdvanceAmount, o.TrackingToken
            FROM dbo.PreOrder o
            WHERE o.CustomerId = @CustomerId AND o.Id <> @ExcludeId AND o.IsActive = 1
              AND o.Status IN (1, 2) AND o.TrackingToken IS NOT NULL
            ORDER BY o.OrderDate DESC, o.Id DESC;
            """, new { CustomerId = customerId, ExcludeId = excludeOrderId, Take = take });

    public Task SetTrackingTokenAsync(int orderId, string token) =>
        ExecuteAsync("UPDATE dbo.PreOrder SET TrackingToken = @Token WHERE Id = @Id;", new { Id = orderId, Token = token });

    public Task SoftDeleteAsync(int id, int userId, DateTime at) =>
        ExecuteAsync("UPDATE dbo.PreOrder SET IsActive = 0, UpdatedBy = @UserId, UpdatedAt = @At WHERE Id = @Id;",
            new { Id = id, UserId = userId, At = at });

    // ───────────── filter helpers (one definition for list, counts and export) ─────────────

    private static string FilterWhere(bool includeStatus) => $"""
        o.IsActive = 1
          AND {ScopeFilter("o")}
          {(includeStatus ? "AND (@Status IS NULL OR o.Status = @Status)" : string.Empty)}
          AND (@BatchId IS NULL OR o.BatchId = @BatchId)
          AND (@CustomerId IS NULL OR o.CustomerId = @CustomerId)
          AND (@FromDate IS NULL OR o.OrderDate >= @FromDate)
          AND (@ToDate IS NULL OR o.OrderDate <= @ToDate)
          AND (@Search IS NULL OR o.OrderNo LIKE @Search OR o.ProductName LIKE @Search
               OR c.CustomerName LIKE @Search OR c.Mobile LIKE @Search)
        """;

    private static DynamicParameters FilterParameters(TenantScope scope, OrderFilter f)
    {
        var p = ScopeParameters(scope);
        p.Add("Status", f.Status is null ? null : (byte?)f.Status);
        p.Add("BatchId", f.BatchId);
        p.Add("CustomerId", f.CustomerId);
        p.Add("FromDate", f.FromDate?.Date);
        p.Add("ToDate", f.ToDate?.Date);
        p.Add("Search", f.SearchPattern);
        return p;
    }
}
