/* =====================================================================
   Ordex - 002 Views & Functions (reusable building blocks for reports)
   ===================================================================== */

/* ---------------------------------------------------------------------
   vw_BatchTotals
   One row per purchase batch with its SGD total (orders + extra stock).
   Returned stock is NOT counted again – its cost is already inside the
   original order.
   PayableBdt = what we must pay for the batch at the batch rate.
   --------------------------------------------------------------------- */
CREATE OR ALTER VIEW dbo.vw_BatchTotals
AS
SELECT
    BatchId        = b.Id,
    b.CompanyId,
    b.BusinessUnitId,
    b.BatchNo,
    b.BatchDate,
    b.ExchangeRate,
    b.IsPaid,
    b.PaidDate,
    b.Notes,
    b.IsActive,
    OrderCount     = o.OrderCount,
    OrderSgd       = o.Sgd,
    OrderBdt       = o.Bdt,
    ExtraCount     = s.ExtraCount,
    ExtraSgd       = s.Sgd,
    ExtraBdt       = s.Bdt,
    TotalSgd       = o.Sgd + s.Sgd,
    TotalBdt       = o.Bdt + s.Bdt,
    PayableBdt     = CAST((o.Sgd + s.Sgd) * b.ExchangeRate AS DECIMAL(18,2))
FROM dbo.PurchaseBatch b
CROSS APPLY (
    SELECT OrderCount = COUNT(*),
           Sgd        = ISNULL(SUM(po.PurchasePriceSgd), 0),
           Bdt        = ISNULL(SUM(po.ProductPriceBdt), 0)
    FROM dbo.PreOrder po
    WHERE po.BatchId = b.Id AND po.IsActive = 1
) o
CROSS APPLY (
    SELECT ExtraCount = COUNT(*),
           Sgd        = ISNULL(SUM(si.PriceSgd), 0),
           Bdt        = ISNULL(SUM(si.PriceBdt), 0)
    FROM dbo.StockItem si
    WHERE si.BatchId = b.Id AND si.SourceType = 1 AND si.IsActive = 1
) s;
GO

/* ---------------------------------------------------------------------
   fn_FinanceSummary
   The single source of truth for money calculations in a date range.
   Used by the dashboard, the monthly trend and the Profit & Loss report.

   Cash method (same as the paper calculation):
       Collection  = Advance + Due collected at delivery + Stock sales - Refunds
       Net Profit  = Collection - Purchase cost (BDT) - Expenses
   Earned method (only what has actually been delivered / sold):
       Earned Profit = Delivered margin (money received - cost)
                     + Money kept on returns (received - refunded)
                     + Stock sale margin - Expenses
   @CompanyId / @BusinessUnitId = 0  ->  all.
   --------------------------------------------------------------------- */
CREATE OR ALTER FUNCTION dbo.fn_FinanceSummary
(
    @CompanyId       INT,
    @BusinessUnitId  INT,
    @FromDate        DATE,
    @ToDate          DATE
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        ord.OrderCount,
        OrderPurchaseSgd   = ord.Sgd,
        OrderPurchaseBdt   = ord.Bdt,
        ord.OrderValue,
        AdvanceCollected   = ord.Advance,
        del.DeliveredCount,
        DueCollected       = del.Collected,
        del.DeliveredSales,
        del.DeliveredMargin,
        ret.ReturnedCount,
        Refunds            = ret.Refunds,
        ReturnRetained     = ret.Retained,
        ExtraPurchaseSgd   = ext.Sgd,
        ExtraPurchaseBdt   = ext.Bdt,
        ss.StockSoldCount,
        StockSales         = ss.Sales,
        StockSalesMargin   = ss.Margin,
        Expenses           = ex.Amount,
        calc.TotalPurchaseSgd,
        calc.TotalPurchaseBdt,
        calc.TotalCollection,
        NetProfit          = calc.TotalCollection - calc.TotalPurchaseBdt - ex.Amount,
        EarnedProfit       = del.DeliveredMargin + ret.Retained + ss.Margin - ex.Amount
    FROM (SELECT Dummy = 1) d
    CROSS APPLY (
        SELECT OrderCount = COUNT(*),
               Sgd        = ISNULL(SUM(o.PurchasePriceSgd), 0),
               Bdt        = ISNULL(SUM(o.ProductPriceBdt), 0),
               OrderValue = ISNULL(SUM(o.SellingPrice), 0),
               Advance    = ISNULL(SUM(o.AdvanceAmount), 0)
        FROM dbo.PreOrder o
        WHERE o.IsActive = 1
          AND (@CompanyId = 0 OR o.CompanyId = @CompanyId)
          AND (@BusinessUnitId = 0 OR o.BusinessUnitId = @BusinessUnitId)
          AND o.OrderDate BETWEEN @FromDate AND @ToDate
    ) ord
    CROSS APPLY (
        SELECT DeliveredCount  = ISNULL(SUM(CASE WHEN o.Status = 3 THEN 1 ELSE 0 END), 0),
               Collected       = ISNULL(SUM(o.CollectedAmount), 0),
               DeliveredSales  = ISNULL(SUM(CASE WHEN o.Status = 3 THEN o.SellingPrice ELSE 0 END), 0),
               -- margin on money actually received (covers discounts given at the door)
               DeliveredMargin = ISNULL(SUM(CASE WHEN o.Status = 3
                                                 THEN o.AdvanceAmount + o.CollectedAmount - o.ProductPriceBdt
                                                 ELSE 0 END), 0)
        FROM dbo.PreOrder o
        WHERE o.IsActive = 1
          AND (@CompanyId = 0 OR o.CompanyId = @CompanyId)
          AND (@BusinessUnitId = 0 OR o.BusinessUnitId = @BusinessUnitId)
          AND o.DeliveredAt >= @FromDate
          AND o.DeliveredAt <  DATEADD(DAY, 1, CAST(@ToDate AS DATETIME2(0)))
    ) del
    CROSS APPLY (
        SELECT ReturnedCount = COUNT(*),
               Refunds       = ISNULL(SUM(o.RefundAmount), 0),
               Retained      = ISNULL(SUM(o.AdvanceAmount + o.CollectedAmount - o.RefundAmount), 0)
        FROM dbo.PreOrder o
        WHERE o.IsActive = 1
          AND o.Status = 4
          AND (@CompanyId = 0 OR o.CompanyId = @CompanyId)
          AND (@BusinessUnitId = 0 OR o.BusinessUnitId = @BusinessUnitId)
          AND o.ReturnedAt >= @FromDate
          AND o.ReturnedAt <  DATEADD(DAY, 1, CAST(@ToDate AS DATETIME2(0)))
    ) ret
    CROSS APPLY (
        SELECT Sgd = ISNULL(SUM(s.PriceSgd), 0),
               Bdt = ISNULL(SUM(s.PriceBdt), 0)
        FROM dbo.StockItem s
        WHERE s.IsActive = 1
          AND s.SourceType = 1
          AND (@CompanyId = 0 OR s.CompanyId = @CompanyId)
          AND (@BusinessUnitId = 0 OR s.BusinessUnitId = @BusinessUnitId)
          AND s.EntryDate BETWEEN @FromDate AND @ToDate
    ) ext
    CROSS APPLY (
        SELECT StockSoldCount = COUNT(*),
               Sales          = ISNULL(SUM(s.SoldPrice), 0),
               Margin         = ISNULL(SUM(s.SoldPrice - s.PriceBdt), 0)
        FROM dbo.StockItem s
        WHERE s.IsActive = 1
          AND s.Status = 2
          AND (@CompanyId = 0 OR s.CompanyId = @CompanyId)
          AND (@BusinessUnitId = 0 OR s.BusinessUnitId = @BusinessUnitId)
          AND s.SoldDate BETWEEN @FromDate AND @ToDate
    ) ss
    CROSS APPLY (
        SELECT Amount = ISNULL(SUM(e.Amount), 0)
        FROM dbo.Expense e
        WHERE e.IsActive = 1
          AND (@CompanyId = 0 OR e.CompanyId = @CompanyId)
          AND (@BusinessUnitId = 0 OR e.BusinessUnitId = @BusinessUnitId)
          AND e.ExpenseDate BETWEEN @FromDate AND @ToDate
    ) ex
    CROSS APPLY (
        SELECT TotalPurchaseSgd = ord.Sgd + ext.Sgd,
               TotalPurchaseBdt = ord.Bdt + ext.Bdt,
               TotalCollection  = ord.Advance + del.Collected + ss.Sales - ret.Refunds
    ) calc
);
GO
