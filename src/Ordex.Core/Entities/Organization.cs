using Ordex.Core.Common;
using Ordex.Core.Enums;

namespace Ordex.Core.Entities;

public sealed class Company : AuditableEntity
{
    public string CompanyCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }

    /// <summary>Sales-currency amount for 1 unit of purchase currency, used when an order has no purchase batch yet.</summary>
    public decimal DefaultExchangeRate { get; set; }

    /// <summary>Currency the company buys in abroad (BDT / USD / SGD). Stored in the *Sgd columns.</summary>
    public string PurchaseCurrency { get; set; } = Currencies.DefaultPurchase;

    /// <summary>Currency customers pay in (BDT / USD / SGD). Stored in the *Bdt / price columns.</summary>
    public string SalesCurrency { get; set; } = Currencies.DefaultSales;
}

public sealed class BusinessUnit : AuditableEntity
{
    public int CompanyId { get; set; }
    public string UnitCode { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
}

public sealed class AppUser : TenantEntity
{
    public string FullName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Staff;
    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
