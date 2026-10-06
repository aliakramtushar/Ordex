using Ordex.Core.Abstractions.Repositories;
using Ordex.Core.Abstractions.Services;
using Ordex.Core.Common;
using Ordex.Core.Models;

namespace Ordex.Core.Services;

/// <summary>
/// Customer order tracking. Public and read-only: the unguessable token in the link
/// is the only key, so nothing here depends on the signed-in user.
/// </summary>
public sealed class TrackingService(IOrderRepository orders) : ITrackingService
{
    public async Task<OrderTrackingResult?> GetAsync(string? token)
    {
        if (!TrackingToken.LooksValid(token))
            return null;

        var order = await orders.GetByTrackingTokenAsync(token!);
        if (order is null)
            return null;

        var others = await orders.GetOpenTrackingByCustomerAsync(order.CustomerId, order.Id);
        return new OrderTrackingResult { Order = order, OtherOrders = others };
    }
}
