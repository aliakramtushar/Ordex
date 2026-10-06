using Ordex.Core.Abstractions;
using Ordex.Core.Abstractions.Repositories;
using Ordex.Core.Abstractions.Services;
using Ordex.Core.Common;
using Ordex.Core.Entities;
using Ordex.Core.Enums;
using Ordex.Core.Models;

namespace Ordex.Core.Services;

public sealed class OrderService(
    IOrderRepository orders,
    ICustomerRepository customers,
    IBatchRepository batches,
    IStockRepository stock,
    IDocumentNumberRepository documentNumbers,
    IScopeService scopeService,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    IClock clock) : IOrderService
{
    private const string OrderPrefix = "ORD";
    private const int ExportPageSize = 500;
    private const int ExportMaxPages = 40; // 20,000 rows

    // ───────────────────────────── Queries ─────────────────────────────

    public Task<PagedResult<OrderListItem>> GetPagedAsync(OrderFilter filter) =>
        orders.GetPagedAsync(user.Scope, filter);

    public Task<IReadOnlyList<OrderStatusCount>> GetStatusCountsAsync(OrderFilter filter) =>
        orders.GetStatusCountsAsync(user.Scope, filter);

    public async Task<IReadOnlyList<OrderListItem>> GetForExportAsync(OrderFilter filter)
    {
        var rows = new List<OrderListItem>();
        filter.PageSize = ExportPageSize;

        for (var page = 1; page <= ExportMaxPages; page++)
        {
            filter.Page = page;
            var result = await orders.GetPagedAsync(user.Scope, filter);
            rows.AddRange(result.Items);
            if (!result.HasNext) break;
        }

        return rows;
    }

    public Task<OrderDetails?> GetDetailsAsync(int id) => orders.GetDetailsAsync(id, user.Scope);

    public async Task<IReadOnlyList<OrderStatusLogItem>> GetHistoryAsync(int id)
    {
        // Load through the scoped query first so history of a foreign order can't be read.
        var order = await orders.GetByIdAsync(id, user.Scope);
        return order is null ? [] : await orders.GetStatusLogAsync(id);
    }

    public Task<OrderInput> NewAsync() => Task.FromResult(new OrderInput { OrderDate = clock.Today });

    public async Task<OrderInput?> GetForEditAsync(int id)
    {
        var d = await orders.GetDetailsAsync(id, user.Scope);
        if (d is null) return null;

        return new OrderInput
        {
            Id = d.Id,
            CompanyId = d.CompanyId,
            BusinessUnitId = d.BusinessUnitId,
            OrderDate = d.OrderDate,
            CustomerName = d.CustomerName,
            Mobile = d.Mobile,
            SocialLink = d.SocialLink,
            DeliveryAddress = d.DeliveryAddress,
            BatchId = d.BatchId,
            ProductName = d.ProductName,
            ProductImage = d.ProductImage,
            ProductLink = d.ProductLink,
            ProductSize = d.ProductSize,
            PurchasePriceSgd = d.PurchasePriceSgd,
            ProductPriceBdt = d.ProductPriceBdt,
            SellingPrice = d.SellingPrice,
            AdvanceAmount = d.AdvanceAmount,
            Notes = d.Notes
        };
    }

    // ───────────────────────────── Create / Edit ─────────────────────────────

    public async Task<ServiceResult<PreOrder>> SaveAsync(OrderInput input, UploadedFile? image)
    {
        if (input.AdvanceAmount > input.SellingPrice)
            return ServiceResult<PreOrder>.Fail(Messages.AdvanceTooHigh);

        // 1. Load / create the order and find out which company + unit it belongs to.
        PreOrder order;
        TenantScope scope;

        if (input.IsNew)
        {
            var resolved = await scopeService.ResolveForCreateAsync(input.CompanyId, input.BusinessUnitId);
            if (!resolved.Succeeded)
                return ServiceResult<PreOrder>.Fail(resolved.Error!);

            scope = resolved.Value;
            order = new PreOrder
            {
                CompanyId = scope.CompanyId,
                BusinessUnitId = scope.BusinessUnitId,
                Status = OrderStatus.PreOrder,
                CreatedBy = user.UserId,
                CreatedAt = clock.Now
            };
        }
        else
        {
            var existing = await orders.GetByIdAsync(input.Id, user.Scope);
            if (existing is null)
                return ServiceResult<PreOrder>.Fail(Messages.NotFound);
            if (!OrderWorkflow.CanEdit(existing.Status))
                return ServiceResult<PreOrder>.Fail(Messages.OrderLocked);

            order = existing;
            scope = new TenantScope(order.CompanyId, order.BusinessUnitId);
            order.UpdatedBy = user.UserId;
            order.UpdatedAt = clock.Now;
        }

        // 2. The batch must belong to the very same business unit.
        if (input.BatchId is { } batchId && await batches.GetByIdAsync(batchId, scope) is null)
            return ServiceResult<PreOrder>.Fail(Messages.InvalidBatch);

        // 3. Upload the image (outside the transaction – file systems don't roll back).
        var imageResult = await ImageChange.PrepareAsync(
            storage, order.ProductImage, input.RemoveImage, image, $"{scope.CompanyId}/orders");
        if (!imageResult.Succeeded)
            return ServiceResult<PreOrder>.Fail(imageResult.Error!);

        var imageChange = imageResult.Value!;
        MapInput(input, order, imageChange.FinalPath);

        // 4. Customer + order number + order + history: all or nothing.
        try
        {
            await unitOfWork.ExecuteAsync(async () =>
            {
                order.CustomerId = await UpsertCustomerAsync(scope, input.CustomerName, input.Mobile, input.SocialLink, input.DeliveryAddress);

                if (order.Id == 0)
                {
                    order.OrderNo = await documentNumbers.NextAsync(scope.CompanyId, OrderPrefix, order.OrderDate);
                    order.TrackingToken = TrackingToken.New();
                    order.Id = await orders.InsertAsync(order);
                    await orders.InsertStatusLogAsync(NewLog(order, null, OrderStatus.PreOrder, "Order created"));
                }
                else
                {
                    await orders.UpdateAsync(order);
                }
            });
        }
        catch
        {
            imageChange.Rollback();
            throw;
        }

        imageChange.Commit();
        return ServiceResult<PreOrder>.Ok(order);
    }

    // ───────────────────────────── New order (one customer, many items) ─────────────────────────────

    public async Task<NewOrdersInput> NewOrdersAsync(int? customerId)
    {
        var input = new NewOrdersInput { OrderDate = clock.Today, Items = [new OrderItemInput()] };

        // Defaults to the company / unit picked in the top bar.
        if (user.Scope.CompanyId != 0) input.CompanyId = user.Scope.CompanyId;
        if (user.Scope.BusinessUnitId != 0) input.BusinessUnitId = user.Scope.BusinessUnitId;

        if (customerId is > 0 && await customers.GetByIdAsync(customerId.Value, user.Scope) is { } c)
        {
            input.CustomerId = c.Id;
            input.CustomerName = c.CustomerName;
            input.Mobile = c.Mobile;
            input.SocialLink = c.SocialLink;
            input.DeliveryAddress = c.Address;
            input.CompanyId = c.CompanyId;
            input.BusinessUnitId = c.BusinessUnitId;
        }

        return input;
    }

    public async Task<ServiceResult<IReadOnlyList<PreOrder>>> CreateManyAsync(NewOrdersInput input, IReadOnlyList<UploadedFile?> images)
    {
        var items = input.Items ?? [];
        if (items.Count == 0)
            return Fail(Messages.NoOrderItems);
        if (items.Count > NewOrdersInput.MaxItems)
            return Fail(string.Format(Messages.TooManyOrderItems, NewOrdersInput.MaxItems));

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].AdvanceAmount > items[i].SellingPrice)
                return Fail(string.Format(Messages.ItemAdvanceTooHigh, i + 1));
        }

        var resolved = await scopeService.ResolveForCreateAsync(input.CompanyId, input.BusinessUnitId);
        if (!resolved.Succeeded)
            return Fail(resolved.Error!);
        var scope = resolved.Value;

        if (input.BatchId is { } batchId && await batches.GetByIdAsync(batchId, scope) is null)
            return Fail(Messages.InvalidBatch);

        // Upload every picture first (outside the transaction); undo them all if anything fails.
        var uploads = new List<ImageChange>(items.Count);
        void RollbackUploads() => uploads.ForEach(u => u.Rollback());

        for (var i = 0; i < items.Count; i++)
        {
            var image = i < images.Count ? images[i] : null;
            var prepared = await ImageChange.PrepareAsync(storage, null, false, image, $"{scope.CompanyId}/orders");
            if (!prepared.Succeeded)
            {
                RollbackUploads();
                return Fail(string.Format(Messages.ItemImageFailed, i + 1, prepared.Error));
            }
            uploads.Add(prepared.Value!);
        }

        var created = new List<PreOrder>(items.Count);
        try
        {
            await unitOfWork.ExecuteAsync(async () =>
            {
                var customerId = await UpsertCustomerAsync(scope, input.CustomerName, input.Mobile, input.SocialLink, input.DeliveryAddress);

                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    var order = new PreOrder
                    {
                        CompanyId = scope.CompanyId,
                        BusinessUnitId = scope.BusinessUnitId,
                        CustomerId = customerId,
                        Status = OrderStatus.PreOrder,
                        OrderDate = input.OrderDate.Date,
                        BatchId = input.BatchId,
                        DeliveryAddress = TextHelper.Clean(input.DeliveryAddress),
                        ProductName = item.ProductName.Trim(),
                        ProductImage = uploads[i].FinalPath,
                        ProductLink = TextHelper.Clean(item.ProductLink),
                        ProductSize = TextHelper.Clean(item.ProductSize),
                        PurchasePriceSgd = item.PurchasePriceSgd,
                        ProductPriceBdt = item.ProductPriceBdt,
                        SellingPrice = item.SellingPrice,
                        AdvanceAmount = item.AdvanceAmount,
                        Notes = TextHelper.Clean(item.Notes),
                        TrackingToken = TrackingToken.New(),
                        CreatedBy = user.UserId,
                        CreatedAt = clock.Now
                    };

                    order.OrderNo = await documentNumbers.NextAsync(scope.CompanyId, OrderPrefix, order.OrderDate);
                    order.Id = await orders.InsertAsync(order);
                    await orders.InsertStatusLogAsync(NewLog(order, null, OrderStatus.PreOrder, "Order created"));
                    created.Add(order);
                }
            });
        }
        catch
        {
            RollbackUploads();
            throw;
        }

        return ServiceResult<IReadOnlyList<PreOrder>>.Ok(created);

        static ServiceResult<IReadOnlyList<PreOrder>> Fail(string error) => ServiceResult<IReadOnlyList<PreOrder>>.Fail(error);
    }

    // ───────────────────────────── Status changes ─────────────────────────────

    public Task<ServiceResult<string>> DispatchAsync(OrderStatusInput input) =>
        ChangeStatusAsync(input.OrderId, OrderStatus.OutForDelivery, input.Remarks ?? "Go for delivery", order =>
        {
            order.DispatchedAt = clock.Now;
            return ServiceResult.Ok();
        });

    public Task<ServiceResult<string>> UndoDispatchAsync(OrderStatusInput input) =>
        ChangeStatusAsync(input.OrderId, OrderStatus.PreOrder, input.Remarks ?? "Moved back to pre-order", order =>
        {
            order.DispatchedAt = null;
            return ServiceResult.Ok();
        });

    public Task<ServiceResult<string>> DeliverAsync(DeliverInput input) =>
        ChangeStatusAsync(input.OrderId, OrderStatus.Delivered, input.Remarks ?? "Delivered", order =>
        {
            order.DeliveredAt = clock.Now;
            order.CollectedAmount = input.CollectedAmount;
            return ServiceResult.Ok();
        });

    public Task<ServiceResult<string>> ReturnAsync(ReturnInput input) =>
        ChangeStatusAsync(input.OrderId, OrderStatus.Returned, $"Returned: {input.Reason}", order =>
        {
            var received = order.AdvanceAmount + order.CollectedAmount;
            if (input.RefundAmount > received)
                return ServiceResult.Fail(Messages.RefundTooHigh);

            order.ReturnedAt = clock.Now;
            order.ReturnReason = input.Reason.Trim();
            order.RefundAmount = input.RefundAmount;
            return ServiceResult.Ok();
        },
        afterUpdate: order => stock.InsertAsync(StockFromReturn(order)));

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var order = await orders.GetByIdAsync(id, user.Scope);
        if (order is null)
            return ServiceResult.Fail(Messages.NotFound);
        if (!OrderWorkflow.CanDelete(order.Status))
            return ServiceResult.Fail(Messages.OrderCannotDelete);

        await orders.SoftDeleteAsync(id, user.UserId, clock.Now);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<string>> ResetTrackingLinkAsync(int id)
    {
        var order = await orders.GetByIdAsync(id, user.Scope);
        if (order is null)
            return ServiceResult<string>.Fail(Messages.NotFound);

        var token = TrackingToken.New();
        await orders.SetTrackingTokenAsync(order.Id, token);
        return ServiceResult<string>.Ok(token);
    }

    /// <summary>
    /// The single path every status change goes through:
    /// check the workflow → apply changes → save with an optimistic status check
    /// → write history → run any extra step (e.g. move to stock) – in one transaction.
    /// </summary>
    private async Task<ServiceResult<string>> ChangeStatusAsync(
        int orderId,
        OrderStatus target,
        string remarks,
        Func<PreOrder, ServiceResult> apply,
        Func<PreOrder, Task>? afterUpdate = null)
    {
        var order = await orders.GetByIdAsync(orderId, user.Scope);
        if (order is null)
            return ServiceResult<string>.Fail(Messages.NotFound);

        var from = order.Status;
        if (!OrderWorkflow.CanMove(from, target))
            return ServiceResult<string>.Fail(Messages.InvalidStatusChange);

        var applied = apply(order);
        if (!applied.Succeeded)
            return ServiceResult<string>.Fail(applied.Error!);

        order.Status = target;
        order.UpdatedBy = user.UserId;
        order.UpdatedAt = clock.Now;

        var saved = await unitOfWork.ExecuteAsync(async () =>
        {
            if (!await orders.UpdateStatusAsync(order, from))
                return false;

            await orders.InsertStatusLogAsync(NewLog(order, from, target, remarks));

            if (afterUpdate is not null)
                await afterUpdate(order);

            return true;
        });

        return saved
            ? ServiceResult<string>.Ok(order.OrderNo)
            : ServiceResult<string>.Fail(Messages.Conflict);
    }

    // ───────────────────────────── Helpers ─────────────────────────────

    /// <summary>Mobile number is the customer key inside a business unit: same mobile = same customer.</summary>
    private async Task<int> UpsertCustomerAsync(TenantScope scope, string name, string mobileText, string? socialLink, string? address)
    {
        var mobile = TextHelper.NormalizeMobile(mobileText);
        var existing = await customers.FindByMobileAsync(scope.CompanyId, scope.BusinessUnitId, mobile);

        if (existing is null)
        {
            return await customers.InsertAsync(new Customer
            {
                CompanyId = scope.CompanyId,
                BusinessUnitId = scope.BusinessUnitId,
                CustomerName = name.Trim(),
                Mobile = mobile,
                SocialLink = TextHelper.Clean(socialLink),
                Address = TextHelper.Clean(address),
                CreatedBy = user.UserId,
                CreatedAt = clock.Now
            });
        }

        // Keep the customer card up to date with the latest details we were given.
        existing.CustomerName = name.Trim();
        existing.SocialLink = TextHelper.Clean(socialLink) ?? existing.SocialLink;
        existing.Address = TextHelper.Clean(address) ?? existing.Address;
        existing.UpdatedBy = user.UserId;
        existing.UpdatedAt = clock.Now;
        await customers.UpdateAsync(existing);
        return existing.Id;
    }

    private static void MapInput(OrderInput input, PreOrder order, string? imagePath)
    {
        order.OrderDate = input.OrderDate.Date;
        order.BatchId = input.BatchId;
        order.DeliveryAddress = TextHelper.Clean(input.DeliveryAddress);
        order.ProductName = input.ProductName.Trim();
        order.ProductImage = imagePath;
        order.ProductLink = TextHelper.Clean(input.ProductLink);
        order.ProductSize = TextHelper.Clean(input.ProductSize);
        order.PurchasePriceSgd = input.PurchasePriceSgd;
        order.ProductPriceBdt = input.ProductPriceBdt;
        order.SellingPrice = input.SellingPrice;
        order.AdvanceAmount = input.AdvanceAmount;
        order.Notes = TextHelper.Clean(input.Notes);
    }

    private OrderStatusLog NewLog(PreOrder order, OrderStatus? from, OrderStatus to, string? remarks) => new()
    {
        CompanyId = order.CompanyId,
        BusinessUnitId = order.BusinessUnitId,
        OrderId = order.Id,
        FromStatus = from,
        ToStatus = to,
        Remarks = remarks is { Length: > 500 } ? remarks[..500] : remarks,
        ChangedBy = user.UserId,
        ChangedAt = clock.Now
    };

    private StockItem StockFromReturn(PreOrder order) => new()
    {
        CompanyId = order.CompanyId,
        BusinessUnitId = order.BusinessUnitId,
        SourceType = StockSource.Return,
        SourceOrderId = order.Id,
        BatchId = order.BatchId,
        EntryDate = clock.Today,
        ProductName = order.ProductName,
        ProductImage = order.ProductImage,
        ProductLink = order.ProductLink,
        ProductSize = order.ProductSize,
        PriceSgd = order.PurchasePriceSgd,
        PriceBdt = order.ProductPriceBdt,
        ReturnReason = order.ReturnReason,
        Status = StockStatus.InStock,
        CreatedBy = user.UserId,
        CreatedAt = clock.Now
    };
}
