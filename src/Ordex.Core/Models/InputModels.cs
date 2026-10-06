using System.ComponentModel.DataAnnotations;
using Ordex.Core.Enums;

namespace Ordex.Core.Models;

/*  Input models = what a form posts. Validation attributes live here, so the
    same rules apply in the browser (unobtrusive validation) and on the server. */

/// <summary>
/// Create forms for users who can see more than one company / unit
/// (SuperAdmin, company Admin) must say where the record belongs.
/// Users bound to one unit never see these fields.
/// </summary>
public abstract class ScopedInput
{
    public int Id { get; set; }

    [Display(Name = "Company")]
    public int? CompanyId { get; set; }

    [Display(Name = "Business unit")]
    public int? BusinessUnitId { get; set; }

    public bool IsNew => Id == 0;
}

public sealed class OrderInput : ScopedInput
{
    [Required, DataType(DataType.Date), Display(Name = "Order date")]
    public DateTime OrderDate { get; set; } = DateTime.Today;

    [Required, StringLength(150), Display(Name = "Customer name")]
    public string CustomerName { get; set; } = string.Empty;

    [Required, StringLength(20, MinimumLength = 6), RegularExpression(@"^[0-9+\-\s]+$", ErrorMessage = "Enter a valid mobile number."), Display(Name = "Mobile number")]
    public string Mobile { get; set; } = string.Empty;

    [StringLength(500), Display(Name = "Social media link")]
    public string? SocialLink { get; set; }

    [StringLength(500), Display(Name = "Delivery address")]
    public string? DeliveryAddress { get; set; }

    [Display(Name = "Purchase batch")]
    public int? BatchId { get; set; }

    [Required, StringLength(200), Display(Name = "Product name")]
    public string ProductName { get; set; } = string.Empty;

    /// <summary>Current image path (kept when no new image is uploaded).</summary>
    public string? ProductImage { get; set; }

    public bool RemoveImage { get; set; }

    [StringLength(500), Display(Name = "Product link")]
    public string? ProductLink { get; set; }

    [StringLength(50), Display(Name = "Size")]
    public string? ProductSize { get; set; }

    [Range(0, 9_999_999), Display(Name = "Purchase price")]
    public decimal PurchasePriceSgd { get; set; }

    [Range(0, 999_999_999), Display(Name = "Product cost")]
    public decimal ProductPriceBdt { get; set; }

    [Range(0, 999_999_999), Display(Name = "Selling price")]
    public decimal SellingPrice { get; set; }

    [Range(0, 999_999_999), Display(Name = "Advance paid")]
    public decimal AdvanceAmount { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// New-order screen: one customer, one or more items. Each item becomes its own
/// pre-order (own number, status and delivery), all saved in one transaction.
/// </summary>
public sealed class NewOrdersInput : ScopedInput
{
    public const int MaxItems = 15;

    /// <summary>Existing customer picked from the search (null = new customer). Hint only – the mobile number is the key.</summary>
    public int? CustomerId { get; set; }

    [Required, DataType(DataType.Date), Display(Name = "Order date")]
    public DateTime OrderDate { get; set; } = DateTime.Today;

    [Required, StringLength(150), Display(Name = "Customer name")]
    public string CustomerName { get; set; } = string.Empty;

    [Required, StringLength(20, MinimumLength = 6), RegularExpression(@"^[0-9+\-\s]+$", ErrorMessage = "Enter a valid mobile number."), Display(Name = "Mobile number")]
    public string Mobile { get; set; } = string.Empty;

    [StringLength(500), Display(Name = "Social media link")]
    public string? SocialLink { get; set; }

    [StringLength(500), Display(Name = "Delivery address")]
    public string? DeliveryAddress { get; set; }

    [Display(Name = "Purchase batch")]
    public int? BatchId { get; set; }

    public List<OrderItemInput> Items { get; set; } = [];
}

/// <summary>One product line on the new-order screen.</summary>
public sealed class OrderItemInput
{
    [Required(ErrorMessage = "Enter the product name."), StringLength(200), Display(Name = "Product name")]
    public string ProductName { get; set; } = string.Empty;

    [StringLength(50), Display(Name = "Size")]
    public string? ProductSize { get; set; }

    [StringLength(500), Display(Name = "Product link")]
    public string? ProductLink { get; set; }

    [Range(0, 9_999_999), Display(Name = "Purchase price")]
    public decimal PurchasePriceSgd { get; set; }

    [Range(0, 999_999_999), Display(Name = "Product cost")]
    public decimal ProductPriceBdt { get; set; }

    [Range(0, 999_999_999), Display(Name = "Selling price")]
    public decimal SellingPrice { get; set; }

    [Range(0, 999_999_999), Display(Name = "Advance paid")]
    public decimal AdvanceAmount { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

/// <summary>What a company Admin may change on his own company (no code, no on/off switch).</summary>
public sealed class CompanyProfileInput
{
    [Required, StringLength(150), Display(Name = "Company name")]
    public string CompanyName { get; set; } = string.Empty;

    [StringLength(30), Phone]
    public string? Phone { get; set; }

    [StringLength(150), EmailAddress]
    public string? Email { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    [Range(0, 100_000), Display(Name = "Default exchange rate")]
    public decimal DefaultExchangeRate { get; set; }

    [Required, RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Please choose a currency."), Display(Name = "Purchase currency")]
    public string PurchaseCurrency { get; set; } = Common.Currencies.DefaultPurchase;

    [Required, RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Please choose a currency."), Display(Name = "Sales currency")]
    public string SalesCurrency { get; set; } = Common.Currencies.DefaultSales;

    /// <summary>Read-only on the page.</summary>
    public string CompanyCode { get; set; } = string.Empty;
}

public sealed class OrderStatusInput
{
    [Required]
    public int OrderId { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }
}

public sealed class DeliverInput
{
    [Required]
    public int OrderId { get; set; }

    [Range(0, 999_999_999), Display(Name = "Amount collected")]
    public decimal CollectedAmount { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }
}

public sealed class ReturnInput
{
    [Required]
    public int OrderId { get; set; }

    [Required(ErrorMessage = "Please write the return reason."), StringLength(500), Display(Name = "Return reason")]
    public string Reason { get; set; } = string.Empty;

    [Range(0, 999_999_999), Display(Name = "Refund to customer")]
    public decimal RefundAmount { get; set; }
}

public sealed class CustomerInput
{
    public int Id { get; set; }

    [Required, StringLength(150), Display(Name = "Customer name")]
    public string CustomerName { get; set; } = string.Empty;

    [Required, StringLength(20, MinimumLength = 6), RegularExpression(@"^[0-9+\-\s]+$", ErrorMessage = "Enter a valid mobile number.")]
    public string Mobile { get; set; } = string.Empty;

    [StringLength(500), Display(Name = "Social media link")]
    public string? SocialLink { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }
}

public sealed class BatchInput : ScopedInput
{
    [StringLength(30), Display(Name = "Batch no")]
    public string? BatchNo { get; set; }

    [Required, DataType(DataType.Date), Display(Name = "Purchase date")]
    public DateTime BatchDate { get; set; } = DateTime.Today;

    [Range(0.0001, 100_000, ErrorMessage = "Enter the exchange rate."), Display(Name = "Exchange rate")]
    public decimal ExchangeRate { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public sealed class StockInput : ScopedInput
{
    [Required, DataType(DataType.Date), Display(Name = "Entry date")]
    public DateTime EntryDate { get; set; } = DateTime.Today;

    [Display(Name = "Purchase batch")]
    public int? BatchId { get; set; }

    [Required, StringLength(200), Display(Name = "Product name")]
    public string ProductName { get; set; } = string.Empty;

    public string? ProductImage { get; set; }

    public bool RemoveImage { get; set; }

    [StringLength(500), Display(Name = "Product link")]
    public string? ProductLink { get; set; }

    [StringLength(50), Display(Name = "Size")]
    public string? ProductSize { get; set; }

    [Range(0, 9_999_999), Display(Name = "Purchase price")]
    public decimal PriceSgd { get; set; }

    [Range(0, 999_999_999), Display(Name = "Cost")]
    public decimal PriceBdt { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public sealed class StockSaleInput
{
    [Required]
    public int Id { get; set; }

    [Range(0.01, 999_999_999, ErrorMessage = "Enter the selling price."), Display(Name = "Sold price")]
    public decimal SoldPrice { get; set; }

    [Required, DataType(DataType.Date), Display(Name = "Sold date")]
    public DateTime SoldDate { get; set; } = DateTime.Today;

    [StringLength(150), Display(Name = "Sold to")]
    public string? SoldTo { get; set; }
}

public sealed class ExpenseInput : ScopedInput
{
    [Required, DataType(DataType.Date), Display(Name = "Date")]
    public DateTime ExpenseDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Please select a category."), Display(Name = "Category")]
    public int? CategoryId { get; set; }

    [Display(Name = "Purchase batch")]
    public int? BatchId { get; set; }

    [Range(0.01, 999_999_999, ErrorMessage = "Enter an amount."), Display(Name = "Amount")]
    public decimal Amount { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }
}

public sealed class ExpenseCategoryInput
{
    public int Id { get; set; }

    [Required, StringLength(100), Display(Name = "Category name")]
    public string CategoryName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

// Currency fields use a pattern, not StringLength: on a <select> jQuery Validate measures
// "length" as the number of selected options (1), so StringLength(3, 3) always failed in the browser.
public sealed class CompanyInput
{
    public int Id { get; set; }

    [Required, StringLength(20), RegularExpression(@"^[A-Za-z0-9\-_]+$", ErrorMessage = "Letters, numbers, - and _ only."), Display(Name = "Code")]
    public string CompanyCode { get; set; } = string.Empty;

    [Required, StringLength(150), Display(Name = "Company name")]
    public string CompanyName { get; set; } = string.Empty;

    [StringLength(30), Phone]
    public string? Phone { get; set; }

    [StringLength(150), EmailAddress]
    public string? Email { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    [Range(0, 100_000), Display(Name = "Default exchange rate")]
    public decimal DefaultExchangeRate { get; set; }

    [Required, RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Please choose a currency."), Display(Name = "Purchase currency")]
    public string PurchaseCurrency { get; set; } = Common.Currencies.DefaultPurchase;

    [Required, RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Please choose a currency."), Display(Name = "Sales currency")]
    public string SalesCurrency { get; set; } = Common.Currencies.DefaultSales;

    public bool IsActive { get; set; } = true;
}

public sealed class BusinessUnitInput
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Please select a company."), Display(Name = "Company")]
    public int? CompanyId { get; set; }

    [Required, StringLength(20), RegularExpression(@"^[A-Za-z0-9\-_]+$", ErrorMessage = "Letters, numbers, - and _ only."), Display(Name = "Code")]
    public string UnitCode { get; set; } = string.Empty;

    [Required, StringLength(150), Display(Name = "Business unit name")]
    public string UnitName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public sealed class UserInput
{
    public int Id { get; set; }

    [Required, StringLength(150), Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(60, MinimumLength = 3), RegularExpression(@"^[A-Za-z0-9._@\-]+$", ErrorMessage = "Letters, numbers and . _ @ - only."), Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [StringLength(150), EmailAddress]
    public string? Email { get; set; }

    [StringLength(30), Phone]
    public string? Phone { get; set; }

    [Required]
    public UserRole Role { get; set; } = UserRole.Staff;

    [Display(Name = "Company")]
    public int CompanyId { get; set; }

    [Display(Name = "Business unit")]
    public int BusinessUnitId { get; set; }

    /// <summary>Required on create; optional on edit (blank = keep current password).</summary>
    [StringLength(100, MinimumLength = 6), DataType(DataType.Password)]
    public string? Password { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class LoginInput
{
    [Required(ErrorMessage = "Enter your username."), StringLength(60), Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password."), StringLength(100), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class ChangePasswordInput
{
    [Required, DataType(DataType.Password), Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password), Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match."), DataType(DataType.Password), Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
