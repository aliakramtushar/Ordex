namespace Ordex.Core.Models;

/// <summary>Maps 1:1 to dbo.fn_FinanceSummary.</summary>
public class FinanceSummary
{
    public int OrderCount { get; set; }
    public decimal OrderPurchaseSgd { get; set; }
    public decimal OrderPurchaseBdt { get; set; }
    public decimal OrderValue { get; set; }
    public decimal AdvanceCollected { get; set; }

    public int DeliveredCount { get; set; }
    public decimal DueCollected { get; set; }
    public decimal DeliveredSales { get; set; }
    public decimal DeliveredMargin { get; set; }

    public int ReturnedCount { get; set; }
    public decimal Refunds { get; set; }

    /// <summary>Advance + collected - refund on returned orders (money we kept).</summary>
    public decimal ReturnRetained { get; set; }

    public decimal ExtraPurchaseSgd { get; set; }
    public decimal ExtraPurchaseBdt { get; set; }

    public int StockSoldCount { get; set; }
    public decimal StockSales { get; set; }
    public decimal StockSalesMargin { get; set; }

    public decimal Expenses { get; set; }

    public decimal TotalPurchaseSgd { get; set; }
    public decimal TotalPurchaseBdt { get; set; }
    public decimal TotalCollection { get; set; }

    /// <summary>Cash method: Collection - Purchase - Expenses.</summary>
    public decimal NetProfit { get; set; }

    /// <summary>Earned method: Delivered margin + money kept on returns + Stock sale margin - Expenses.</summary>
    public decimal EarnedProfit { get; set; }
}

public sealed class DashboardKpi : FinanceSummary
{
    public int PendingCount { get; set; }
    public int OutForDeliveryCount { get; set; }
    public decimal DueReceivable { get; set; }
    public int StockInHandCount { get; set; }
    public decimal StockInHandValue { get; set; }
    public decimal UnpaidSgd { get; set; }
    public decimal UnpaidBdt { get; set; }
    public int UnpaidBatchCount { get; set; }
}

public sealed class MonthlyTrendItem
{
    public DateTime MonthStart { get; set; }
    public decimal TotalCollection { get; set; }
    public decimal TotalPurchaseSgd { get; set; }
    public decimal TotalPurchaseBdt { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetProfit { get; set; }
    public decimal EarnedProfit { get; set; }
}

public sealed class RecentOrderItem
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductImage { get; set; }
    public string? ProductSize { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal AdvanceAmount { get; set; }
    public decimal DueAmount { get; set; }
    public Enums.OrderStatus Status { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
}

public sealed class DashboardData
{
    public DashboardKpi Kpi { get; init; } = new();
    public IReadOnlyList<MonthlyTrendItem> Trend { get; init; } = [];
    public IReadOnlyList<RecentOrderItem> RecentOrders { get; init; } = [];
}

public sealed class ExpenseByCategory
{
    public string CategoryName { get; set; } = string.Empty;
    public int EntryCount { get; set; }
    public decimal Amount { get; set; }
}

public sealed class ProfitLossReport
{
    public FinanceSummary Summary { get; init; } = new();
    public IReadOnlyList<ExpenseByCategory> ExpenseByCategory { get; init; } = [];
    public IReadOnlyList<BatchListItem> Batches { get; init; } = [];
}

public sealed class PayableMonth
{
    public int MonthNo { get; set; }
    public DateTime MonthStart { get; set; }
    public int BatchCount { get; set; }
    public decimal TotalSgd { get; set; }
    public decimal PayableBdt { get; set; }
    public decimal PaidSgd { get; set; }
    public decimal UnpaidSgd { get; set; }
    public decimal UnpaidBdt { get; set; }
}

public sealed class UnassignedOrders
{
    public int OrderCount { get; set; }
    public decimal TotalSgd { get; set; }
    public decimal TotalBdt { get; set; }
}

public sealed class PayableReport
{
    public int Year { get; init; }
    public IReadOnlyList<PayableMonth> Months { get; init; } = [];
    public IReadOnlyList<BatchListItem> Batches { get; init; } = [];
    public UnassignedOrders Unassigned { get; init; } = new();
}
