using Ordex.Core.Common;
using Ordex.Core.Enums;

namespace Ordex.Core.Entities;

public sealed class Customer : TenantEntity
{
    public string CustomerName { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string? SocialLink { get; set; }
    public string? Address { get; set; }
}

/// <summary>One buying trip abroad, priced at that day's SGD -> BDT rate.</summary>
public sealed class PurchaseBatch : TenantEntity
{
    public string BatchNo { get; set; } = string.Empty;
    public DateTime BatchDate { get; set; }
    public decimal ExchangeRate { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidDate { get; set; }
    public string? Notes { get; set; }
}

public sealed class PreOrder : TenantEntity
{
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>Secret part of the customer's tracking link (/t/{token}). Random, unguessable, can be reset.</summary>
    public string? TrackingToken { get; set; }
    public DateTime OrderDate { get; set; }
    public int CustomerId { get; set; }
    public int? BatchId { get; set; }
    public string? DeliveryAddress { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public string? ProductImage { get; set; }
    public string? ProductLink { get; set; }
    public string? ProductSize { get; set; }

    public decimal PurchasePriceSgd { get; set; }
    public decimal ProductPriceBdt { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal AdvanceAmount { get; set; }

    /// <summary>Computed in SQL: SellingPrice - AdvanceAmount.</summary>
    public decimal DueAmount { get; set; }

    /// <summary>Computed in SQL: SellingPrice - ProductPriceBdt.</summary>
    public decimal Profit { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.PreOrder;
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public decimal CollectedAmount { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? ReturnReason { get; set; }
    public decimal RefundAmount { get; set; }
    public string? Notes { get; set; }
}

public sealed class OrderStatusLog
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BusinessUnitId { get; set; }
    public int OrderId { get; set; }
    public OrderStatus? FromStatus { get; set; }
    public OrderStatus ToStatus { get; set; }
    public string? Remarks { get; set; }
    public int ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
}

public sealed class StockItem : TenantEntity
{
    public StockSource SourceType { get; set; }
    public int? SourceOrderId { get; set; }
    public int? BatchId { get; set; }
    public DateTime EntryDate { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductImage { get; set; }
    public string? ProductLink { get; set; }
    public string? ProductSize { get; set; }
    public decimal PriceSgd { get; set; }
    public decimal PriceBdt { get; set; }
    public string? ReturnReason { get; set; }
    public StockStatus Status { get; set; } = StockStatus.InStock;
    public decimal? SoldPrice { get; set; }
    public DateTime? SoldDate { get; set; }
    public string? SoldTo { get; set; }
    public string? Notes { get; set; }
}

public sealed class ExpenseCategory : TenantEntity
{
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>Categories with CompanyId = 0 are shared by every company.</summary>
    public bool IsShared => CompanyId == 0;
}

public sealed class Expense : TenantEntity
{
    public DateTime ExpenseDate { get; set; }
    public int CategoryId { get; set; }
    public int? BatchId { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}
