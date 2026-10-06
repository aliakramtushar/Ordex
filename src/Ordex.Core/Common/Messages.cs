namespace Ordex.Core.Common;

/// <summary>
/// One place for every user-facing message, so wording stays consistent
/// (and can be translated later without hunting through the code).
/// </summary>
public static class Messages
{
    // Generic
    public const string Saved = "Saved successfully.";
    public const string Updated = "Changes saved.";
    public const string Deleted = "Deleted successfully.";
    public const string NotFound = "The record was not found or you don't have access to it.";
    public const string AccessDenied = "You don't have permission to do that.";
    public const string ValidationFailed = "Please fix the highlighted fields.";
    public const string UnexpectedError = "Something went wrong. Please try again. If it keeps happening, contact your administrator.";
    public const string Conflict = "This record was changed by someone else. Please refresh and try again.";
    public const string TooManyRequests = "Too many requests. Please wait a moment and try again.";
    public const string DuplicateCode = "This code is already in use.";
    public const string InvalidCurrency = "Please choose BDT, USD or SGD.";

    // Scope
    public const string SelectCompany = "Please select a company.";
    public const string SelectBusinessUnit = "Please select a business unit.";
    public const string InvalidBusinessUnit = "The selected business unit is not valid for this company.";

    // Auth
    public const string InvalidLogin = "Invalid username or password.";
    public const string AccountLocked = "Too many failed attempts. Please try again after {0} minutes.";
    public const string AccountInactive = "Your account is inactive. Please contact your administrator.";
    public const string PasswordChanged = "Password changed successfully.";
    public const string WrongCurrentPassword = "Current password is incorrect.";
    public const string AppearanceSaved = "Your look is saved.";
    public const string InvalidAppearance = "Please choose one of the listed themes.";
    public const string UserNameTaken = "This username is already taken.";

    // Orders
    public const string OrderCreated = "Order {0} created.";
    public const string OrderDispatched = "Order {0} is out for delivery.";
    public const string OrderDelivered = "Order {0} marked as delivered.";
    public const string OrderReturned = "Order {0} returned and moved to stock.";
    public const string OrderReverted = "Order {0} moved back to pre-order.";
    public const string OrderLocked = "Returned orders can't be edited.";
    public const string OrderCannotDelete = "Only pre-orders can be deleted.";
    public const string InvalidStatusChange = "This status change is not allowed.";
    public const string AdvanceTooHigh = "Advance can't be more than the selling price.";
    public const string RefundTooHigh = "Refund can't be more than the amount received.";
    public const string OrdersCreated = "{0} orders created for {1}.";
    public const string NoOrderItems = "Add at least one product.";
    public const string TooManyOrderItems = "You can add up to {0} products at a time.";
    public const string ItemAdvanceTooHigh = "Product {0}: advance can't be more than the selling price.";
    public const string ItemImageFailed = "Product {0}: {1}";
    public const string TrackingLinkReset = "New customer link created. The old link no longer works.";
    public const string InvalidBatch = "The selected batch is not valid for this business unit.";

    // Organisation
    public const string CompanyHasData = "This company already has data (business units, users or records). Switch it off instead of deleting.";
    public const string UnitHasData = "This business unit already has data (users, orders, stock or expenses). Switch it off instead of deleting.";
    public const string Activated = "{0} is now active.";
    public const string Deactivated = "{0} is now inactive.";

    // Stock
    public const string StockSold = "Item marked as sold.";
    public const string StockAlreadySold = "This item is already sold.";
    public const string StockReturnLocked = "Returned items come from orders and can't be edited here.";

    // Files
    public const string ImageTooLarge = "Image must be smaller than {0} MB.";
    public const string ImageInvalid = "Only JPG, PNG or WEBP images are allowed.";
}
