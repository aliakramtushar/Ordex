using Ordex.Core.Common;
using Ordex.Core.Entities;
using Ordex.Core.Enums;
using Ordex.Core.Models;
using Ordex.Web.Infrastructure;

namespace Ordex.Web.Models;

/*  Page view models: what a screen needs to render.
    Forms bind straight to the Core input models (OrderInput, BatchInput, ...). */

public sealed class DateRange
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }

    public static DateRange Resolve(DateTime? from, DateTime? to, DateTime today)
    {
        var start = from?.Date ?? new DateTime(today.Year, today.Month, 1);
        var end = to?.Date ?? today;
        return end < start ? new DateRange { From = end, To = start } : new DateRange { From = start, To = end };
    }
}

public sealed class DashboardViewModel
{
    public required DashboardData Data { get; init; }
    public required DateRange Range { get; init; }
}

public sealed class OrderIndexViewModel
{
    public required OrderFilter Filter { get; init; }
    public required PagedResult<OrderListItem> Result { get; init; }
    public required IReadOnlyDictionary<OrderStatus, int> Counts { get; init; }
    public required IReadOnlyList<BatchLookupItem> Batches { get; init; }

    public int TotalCount => Counts.Values.Sum();
    public int Count(OrderStatus status) => Counts.TryGetValue(status, out var n) ? n : 0;
}

public sealed class OrderDetailsViewModel
{
    public required OrderDetails Order { get; init; }
    public required IReadOnlyList<OrderStatusLogItem> History { get; init; }
}

public sealed class StockIndexViewModel
{
    public required StockFilter Filter { get; init; }
    public required PagedResult<StockListItem> Result { get; init; }
    public required StockTabCounts Counts { get; init; }
}

public sealed class BatchIndexViewModel
{
    public required BatchFilter Filter { get; init; }
    public required PagedResult<BatchListItem> Result { get; init; }
}

public sealed class BatchDetailsViewModel
{
    public required BatchListItem Batch { get; init; }
    public required PagedResult<OrderListItem> Orders { get; init; }
}

public sealed class ExpenseIndexViewModel
{
    public required ExpenseFilter Filter { get; init; }
    public required PagedResult<ExpenseListItem> Result { get; init; }
    public required decimal TotalAmount { get; init; }
    public required IReadOnlyList<LookupItem> Categories { get; init; }
}

public sealed class CategoriesViewModel
{
    public required IReadOnlyList<ExpenseCategory> Categories { get; init; }
    public ExpenseCategoryInput Input { get; init; } = new();
}

public sealed class CustomerIndexViewModel
{
    public required CustomerFilter Filter { get; init; }
    public required PagedResult<CustomerListItem> Result { get; init; }
}

public sealed class CustomerDetailsViewModel
{
    public required Customer Customer { get; init; }
    public required PagedResult<OrderListItem> Orders { get; init; }
}

public sealed class ProfitLossViewModel
{
    public required ProfitLossReport Report { get; init; }
    public required DateRange Range { get; init; }
}

public sealed class UserIndexViewModel
{
    public required IReadOnlyList<UserListItem> Users { get; init; }
    public string? Search { get; init; }
}

/// <summary>Data for the reusable pager partial.</summary>
public sealed class PagerModel
{
    public required int Page { get; init; }
    public required int TotalPages { get; init; }
    public required int TotalCount { get; init; }
    public required int FirstItem { get; init; }
    public required int LastItem { get; init; }

    /// <summary>Builds the link for a page while keeping all current filters.</summary>
    public required Func<int, string> PageUrl { get; init; }

    public static PagerModel From<T>(PagedResult<T> result, Func<int, string> pageUrl) => new()
    {
        Page = result.Page,
        TotalPages = result.TotalPages,
        TotalCount = result.TotalCount,
        FirstItem = result.FirstItemNumber,
        LastItem = result.LastItemNumber,
        PageUrl = pageUrl
    };
}

/// <summary>Shared modal forms for order status actions (list + details pages).</summary>
public sealed class OrderActionsModel
{
    public required string ReturnUrl { get; init; }
}

public sealed class TrackViewModel
{
    public required Ordex.Core.Models.OrderTrackingResult Result { get; init; }

    /// <summary>Formats amounts in the order's own company currency (the page has no signed-in user).</summary>
    public required Money Money { get; init; }
}
