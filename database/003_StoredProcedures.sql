/* =====================================================================
   Ordex - 003 Reporting stored procedures
   All procedures accept @CompanyId / @BusinessUnitId (0 = all).
   ===================================================================== */

/* ---------------------------------------------------------------------
   usp_Dashboard_Summary
   RS1 : KPI row for the selected period + live pipeline numbers
   RS2 : last 6 months trend (ending at @ToDate's month)
   RS3 : latest orders
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_Summary
    @CompanyId       INT,
    @BusinessUnitId  INT,
    @FromDate        DATE,
    @ToDate          DATE
AS
BEGIN
    SET NOCOUNT ON;

    /* RS1 – KPIs */
    SELECT
        f.*,
        pipe.PendingCount,
        pipe.OutForDeliveryCount,
        pipe.DueReceivable,
        stk.StockInHandCount,
        stk.StockInHandValue,
        pay.UnpaidSgd,
        pay.UnpaidBdt,
        pay.UnpaidBatchCount
    FROM dbo.fn_FinanceSummary(@CompanyId, @BusinessUnitId, @FromDate, @ToDate) f
    CROSS APPLY (
        SELECT PendingCount        = ISNULL(SUM(CASE WHEN o.Status = 1 THEN 1 ELSE 0 END), 0),
               OutForDeliveryCount = ISNULL(SUM(CASE WHEN o.Status = 2 THEN 1 ELSE 0 END), 0),
               DueReceivable       = ISNULL(SUM(o.DueAmount), 0)
        FROM dbo.PreOrder o
        WHERE o.IsActive = 1
          AND o.Status IN (1, 2)
          AND (@CompanyId = 0 OR o.CompanyId = @CompanyId)
          AND (@BusinessUnitId = 0 OR o.BusinessUnitId = @BusinessUnitId)
    ) pipe
    CROSS APPLY (
        SELECT StockInHandCount = COUNT(*),
               StockInHandValue = ISNULL(SUM(s.PriceBdt), 0)
        FROM dbo.StockItem s
        WHERE s.IsActive = 1
          AND s.Status = 1
          AND (@CompanyId = 0 OR s.CompanyId = @CompanyId)
          AND (@BusinessUnitId = 0 OR s.BusinessUnitId = @BusinessUnitId)
    ) stk
    CROSS APPLY (
        SELECT UnpaidSgd        = ISNULL(SUM(b.TotalSgd), 0),
               UnpaidBdt        = ISNULL(SUM(b.PayableBdt), 0),
               UnpaidBatchCount = COUNT(*)
        FROM dbo.vw_BatchTotals b
        WHERE b.IsActive = 1
          AND b.IsPaid = 0
          AND (@CompanyId = 0 OR b.CompanyId = @CompanyId)
          AND (@BusinessUnitId = 0 OR b.BusinessUnitId = @BusinessUnitId)
    ) pay;

    /* RS2 – 6 month trend */
    ;WITH Months AS
    (
        SELECT MonthStart = DATEADD(MONTH, -v.n, DATEFROMPARTS(YEAR(@ToDate), MONTH(@ToDate), 1))
        FROM (VALUES (0), (1), (2), (3), (4), (5)) v(n)
    )
    SELECT
        m.MonthStart,
        f.TotalCollection,
        f.TotalPurchaseSgd,
        f.TotalPurchaseBdt,
        f.Expenses,
        f.NetProfit,
        f.EarnedProfit
    FROM Months m
    CROSS APPLY dbo.fn_FinanceSummary(@CompanyId, @BusinessUnitId, m.MonthStart, EOMONTH(m.MonthStart)) f
    ORDER BY m.MonthStart;

    /* RS3 – latest orders */
    SELECT TOP (6)
        o.Id, o.OrderNo, o.OrderDate, o.ProductName, o.ProductImage, o.ProductSize,
        o.SellingPrice, o.AdvanceAmount, o.DueAmount, o.Status,
        c.CustomerName, c.Mobile
    FROM dbo.PreOrder o
    INNER JOIN dbo.Customer c ON c.Id = o.CustomerId
    WHERE o.IsActive = 1
      AND (@CompanyId = 0 OR o.CompanyId = @CompanyId)
      AND (@BusinessUnitId = 0 OR o.BusinessUnitId = @BusinessUnitId)
    ORDER BY o.Id DESC;
END
GO

/* ---------------------------------------------------------------------
   usp_Report_ProfitLoss
   RS1 : finance summary for the period
   RS2 : expenses by category
   RS3 : purchase batches dated inside the period
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_Report_ProfitLoss
    @CompanyId       INT,
    @BusinessUnitId  INT,
    @FromDate        DATE,
    @ToDate          DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT * FROM dbo.fn_FinanceSummary(@CompanyId, @BusinessUnitId, @FromDate, @ToDate);

    SELECT
        CategoryName = c.CategoryName,
        EntryCount   = COUNT(*),
        Amount       = SUM(e.Amount)
    FROM dbo.Expense e
    INNER JOIN dbo.ExpenseCategory c ON c.Id = e.CategoryId
    WHERE e.IsActive = 1
      AND (@CompanyId = 0 OR e.CompanyId = @CompanyId)
      AND (@BusinessUnitId = 0 OR e.BusinessUnitId = @BusinessUnitId)
      AND e.ExpenseDate BETWEEN @FromDate AND @ToDate
    GROUP BY c.CategoryName
    ORDER BY Amount DESC;

    SELECT
        b.BatchId, b.BatchNo, b.BatchDate, b.ExchangeRate, b.IsPaid, b.PaidDate,
        b.OrderCount, b.ExtraCount, b.TotalSgd, b.TotalBdt, b.PayableBdt
    FROM dbo.vw_BatchTotals b
    WHERE b.IsActive = 1
      AND (@CompanyId = 0 OR b.CompanyId = @CompanyId)
      AND (@BusinessUnitId = 0 OR b.BusinessUnitId = @BusinessUnitId)
      AND b.BatchDate BETWEEN @FromDate AND @ToDate
    ORDER BY b.BatchDate, b.BatchId;
END
GO

/* ---------------------------------------------------------------------
   usp_Report_MonthlyPayable
   How much SGD we bought each month and how much is still unpaid.
   RS1 : 12 month summary for @Year
   RS2 : batch list for @Year
   RS3 : orders that are not yet assigned to any batch (not payable yet)
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_Report_MonthlyPayable
    @CompanyId       INT,
    @BusinessUnitId  INT,
    @Year            INT
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH Months AS
    (
        SELECT MonthNo = v.n
        FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) v(n)
    )
    SELECT
        m.MonthNo,
        MonthStart  = DATEFROMPARTS(@Year, m.MonthNo, 1),
        BatchCount  = COUNT(b.BatchId),
        TotalSgd    = ISNULL(SUM(b.TotalSgd), 0),
        PayableBdt  = ISNULL(SUM(b.PayableBdt), 0),
        PaidSgd     = ISNULL(SUM(CASE WHEN b.IsPaid = 1 THEN b.TotalSgd   END), 0),
        UnpaidSgd   = ISNULL(SUM(CASE WHEN b.IsPaid = 0 THEN b.TotalSgd   END), 0),
        UnpaidBdt   = ISNULL(SUM(CASE WHEN b.IsPaid = 0 THEN b.PayableBdt END), 0)
    FROM Months m
    LEFT JOIN dbo.vw_BatchTotals b
           ON  b.IsActive = 1
           AND YEAR(b.BatchDate)  = @Year
           AND MONTH(b.BatchDate) = m.MonthNo
           AND (@CompanyId = 0 OR b.CompanyId = @CompanyId)
           AND (@BusinessUnitId = 0 OR b.BusinessUnitId = @BusinessUnitId)
    GROUP BY m.MonthNo
    ORDER BY m.MonthNo;

    SELECT
        b.BatchId, b.BatchNo, b.BatchDate, b.ExchangeRate, b.IsPaid, b.PaidDate,
        b.OrderCount, b.ExtraCount, b.TotalSgd, b.TotalBdt, b.PayableBdt
    FROM dbo.vw_BatchTotals b
    WHERE b.IsActive = 1
      AND YEAR(b.BatchDate) = @Year
      AND (@CompanyId = 0 OR b.CompanyId = @CompanyId)
      AND (@BusinessUnitId = 0 OR b.BusinessUnitId = @BusinessUnitId)
    ORDER BY b.BatchDate DESC, b.BatchId DESC;

    SELECT
        OrderCount = COUNT(*),
        TotalSgd   = ISNULL(SUM(o.PurchasePriceSgd), 0),
        TotalBdt   = ISNULL(SUM(o.ProductPriceBdt), 0)
    FROM dbo.PreOrder o
    WHERE o.IsActive = 1
      AND o.BatchId IS NULL
      AND (@CompanyId = 0 OR o.CompanyId = @CompanyId)
      AND (@BusinessUnitId = 0 OR o.BusinessUnitId = @BusinessUnitId);
END
GO
