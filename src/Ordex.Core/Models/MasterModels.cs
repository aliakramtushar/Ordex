using Ordex.Core.Common;
using Ordex.Core.Enums;

namespace Ordex.Core.Models;

/// <summary>Simple Id / Text pair for dropdowns.</summary>
public sealed class LookupItem
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
}

public sealed class BusinessUnitListItem
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string UnitCode { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class UserFilter : PagedQuery
{
}

public sealed class UserListItem
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public UserRole Role { get; set; }
    public int CompanyId { get; set; }
    public int BusinessUnitId { get; set; }
    public string? CompanyName { get; set; }
    public string? BusinessUnitName { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public sealed class CustomerFilter : PagedQuery;

public sealed class CustomerListItem
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BusinessUnitId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string? SocialLink { get; set; }
    public string? Address { get; set; }
    public string? BusinessUnitName { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSpent { get; set; }

    /// <summary>Most recent order date (only filled by the quick search).</summary>
    public DateTime? LastOrderDate { get; set; }
}

public sealed class BatchFilter : PagedQuery
{
    public bool? IsPaid { get; set; }
}

/// <summary>A batch with its totals (from vw_BatchTotals).</summary>
public sealed class BatchListItem
{
    public int BatchId { get; set; }
    public int CompanyId { get; set; }
    public int BusinessUnitId { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public DateTime BatchDate { get; set; }
    public decimal ExchangeRate { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidDate { get; set; }
    public string? Notes { get; set; }
    public int OrderCount { get; set; }
    public int ExtraCount { get; set; }
    public decimal OrderSgd { get; set; }
    public decimal ExtraSgd { get; set; }
    public decimal TotalSgd { get; set; }
    public decimal TotalBdt { get; set; }
    public decimal PayableBdt { get; set; }
    public string? BusinessUnitName { get; set; }
}

public sealed class BatchLookupItem
{
    public int Id { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public DateTime BatchDate { get; set; }
    public decimal ExchangeRate { get; set; }
    public bool IsPaid { get; set; }
}

public enum StockTab
{
    InStock = 0,
    Extra = 1,
    Returned = 2,
    Sold = 3
}

public sealed class StockFilter : PagedQuery
{
    public StockTab Tab { get; set; } = StockTab.InStock;
}

public sealed class StockListItem
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BusinessUnitId { get; set; }
    public StockSource SourceType { get; set; }
    public int? SourceOrderId { get; set; }
    public string? SourceOrderNo { get; set; }
    public int? BatchId { get; set; }
    public string? BatchNo { get; set; }
    public DateTime EntryDate { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductImage { get; set; }
    public string? ProductLink { get; set; }
    public string? ProductSize { get; set; }
    public decimal PriceSgd { get; set; }
    public decimal PriceBdt { get; set; }
    public string? ReturnReason { get; set; }
    public StockStatus Status { get; set; }
    public decimal? SoldPrice { get; set; }
    public DateTime? SoldDate { get; set; }
    public string? SoldTo { get; set; }
    public string? Notes { get; set; }
    public string? BusinessUnitName { get; set; }
}

public sealed class StockTabCounts
{
    public int InStock { get; set; }
    public int Extra { get; set; }
    public int Returned { get; set; }
    public int Sold { get; set; }
    public decimal InStockValue { get; set; }
}

public sealed class ExpenseFilter : PagedQuery
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? CategoryId { get; set; }
}

public sealed class ExpenseListItem
{
    public int Id { get; set; }
    public DateTime ExpenseDate { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string? BatchNo { get; set; }
    public string? BusinessUnitName { get; set; }
}
