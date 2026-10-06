namespace Ordex.Core.Enums;

public enum UserRole : byte
{
    SuperAdmin = 1,
    Admin = 2,
    Staff = 3
}

public enum OrderStatus : byte
{
    PreOrder = 1,
    OutForDelivery = 2,
    Delivered = 3,
    Returned = 4
}

public enum StockSource : byte
{
    Extra = 1,
    Return = 2
}

public enum StockStatus : byte
{
    InStock = 1,
    Sold = 2
}

public static class EnumText
{
    public static string ToText(this OrderStatus status) => status switch
    {
        OrderStatus.PreOrder => "Pre-order",
        OrderStatus.OutForDelivery => "Out for delivery",
        OrderStatus.Delivered => "Delivered",
        OrderStatus.Returned => "Returned",
        _ => status.ToString()
    };

    public static string ToText(this UserRole role) => role switch
    {
        UserRole.SuperAdmin => "Super Admin",
        UserRole.Admin => "Admin",
        UserRole.Staff => "Staff",
        _ => role.ToString()
    };

    public static string ToText(this StockSource source) => source switch
    {
        StockSource.Extra => "Extra purchase",
        StockSource.Return => "Customer return",
        _ => source.ToString()
    };
}
