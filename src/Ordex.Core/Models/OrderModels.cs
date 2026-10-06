using Ordex.Core.Common;
using Ordex.Core.Enums;

namespace Ordex.Core.Models;

public sealed class OrderFilter : PagedQuery
{
    public OrderStatus? Status { get; set; }
    public int? BatchId { get; set; }
    public int? CustomerId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class OrderListItem
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BusinessUnitId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }

    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string? SocialLink { get; set; }
    public string? DeliveryAddress { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public string? ProductImage { get; set; }
    public string? ProductSize { get; set; }

    public decimal PurchasePriceSgd { get; set; }
    public decimal ProductPriceBdt { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal AdvanceAmount { get; set; }
    public decimal DueAmount { get; set; }
    public decimal Profit { get; set; }
    public decimal CollectedAmount { get; set; }

    public OrderStatus Status { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? ReturnReason { get; set; }

    public int? BatchId { get; set; }
    public string? TrackingToken { get; set; }
    public string? BatchNo { get; set; }
    public string? BusinessUnitName { get; set; }
}

public sealed class OrderDetails : OrderListItem
{
    public string? ProductLink { get; set; }
    public string? Notes { get; set; }
    public decimal? ExchangeRate { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public decimal RefundAmount { get; set; }
    public string? CompanyName { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class OrderStatusLogItem
{
    public OrderStatus? FromStatus { get; set; }
    public OrderStatus ToStatus { get; set; }
    public string? Remarks { get; set; }
    public string? ChangedByName { get; set; }
    public DateTime ChangedAt { get; set; }
}

public sealed class OrderStatusCount
{
    public OrderStatus Status { get; set; }
    public int Total { get; set; }
}

/// <summary>What the customer sees on the public tracking page – no cost, profit or internal notes.</summary>
public sealed class OrderTrackingView
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BusinessUnitId { get; set; }
    public int CustomerId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? ProductImage { get; set; }
    public string? ProductSize { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal AdvanceAmount { get; set; }
    public decimal CollectedAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyPhone { get; set; }
    public string? BusinessUnitName { get; set; }
    public string PurchaseCurrency { get; set; } = "SGD";
    public string SalesCurrency { get; set; } = "BDT";
    public string TrackingToken { get; set; } = string.Empty;

    public decimal Paid => AdvanceAmount + CollectedAmount - RefundAmount;
    public decimal Due => Status is OrderStatus.PreOrder or OrderStatus.OutForDelivery ? Math.Max(0, SellingPrice - AdvanceAmount) : 0;
}

/// <summary>The same customer's other open orders, listed under the main one.</summary>
public sealed class OrderTrackingSummary
{
    public string OrderNo { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductSize { get; set; }
    public string? ProductImage { get; set; }
    public OrderStatus Status { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal AdvanceAmount { get; set; }
    public string TrackingToken { get; set; } = string.Empty;
}

public sealed class OrderTrackingResult
{
    public required OrderTrackingView Order { get; init; }
    public IReadOnlyList<OrderTrackingSummary> OtherOrders { get; init; } = [];
}
