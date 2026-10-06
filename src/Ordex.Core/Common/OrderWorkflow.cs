using Ordex.Core.Enums;

namespace Ordex.Core.Common;

/// <summary>
/// The only place that decides which status an order can move to.
///
///   Pre-order ──Go for delivery──► Out for delivery ──Delivered──► Delivered
///        ▲                              │      │                      │
///        └──────────Undo────────────────┘      └──────Return──────────┴──► Returned (→ Stock)
/// </summary>
public static class OrderWorkflow
{
    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> Transitions =
        new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.PreOrder] = [OrderStatus.OutForDelivery],
            [OrderStatus.OutForDelivery] = [OrderStatus.Delivered, OrderStatus.Returned, OrderStatus.PreOrder],
            [OrderStatus.Delivered] = [OrderStatus.Returned],
            [OrderStatus.Returned] = []
        };

    public static bool CanMove(OrderStatus from, OrderStatus to) =>
        Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static bool CanDispatch(OrderStatus status) => CanMove(status, OrderStatus.OutForDelivery);
    public static bool CanDeliver(OrderStatus status) => CanMove(status, OrderStatus.Delivered);
    public static bool CanReturn(OrderStatus status) => CanMove(status, OrderStatus.Returned);
    public static bool CanUndoDispatch(OrderStatus status) => CanMove(status, OrderStatus.PreOrder);

    /// <summary>Returned orders are closed – their item now lives in stock.</summary>
    public static bool CanEdit(OrderStatus status) => status != OrderStatus.Returned;

    /// <summary>Only orders that have not left the shop can be deleted.</summary>
    public static bool CanDelete(OrderStatus status) => status == OrderStatus.PreOrder;
}
