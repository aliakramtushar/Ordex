using Microsoft.AspNetCore.Mvc;
using Ordex.Core.Abstractions.Services;
using Ordex.Core.Common;
using Ordex.Core.Enums;
using Ordex.Core.Models;
using Ordex.Web.Infrastructure;
using Ordex.Web.Models;

namespace Ordex.Web.Controllers;

public sealed class OrdersController(IOrderService orders, ILookupService lookups) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(OrderFilter filter)
    {
        var result = await orders.GetPagedAsync(filter);
        var counts = await orders.GetStatusCountsAsync(filter);
        var batches = await lookups.BatchesAsync(null, null);

        return View(new OrderIndexViewModel
        {
            Filter = filter,
            Result = result,
            Counts = counts.ToDictionary(c => c.Status, c => c.Total),
            Batches = batches
        });
    }

    /// <summary>Same filters as the list, downloaded as a CSV that opens in Excel.</summary>
    [HttpGet]
    public async Task<IActionResult> Export(OrderFilter filter)
    {
        var rows = await orders.GetForExportAsync(filter);

        var csv = new CsvBuilder().Row("Order No", "Order Date", "Customer", "Mobile", "Social Link", "Delivery Address",
            "Product", "Size", "Batch", "Purchase Price", "Cost", "Selling Price", "Advance", "Due", "Profit",
            "Status", "Collected", "Return Reason");

        foreach (var o in rows)
        {
            csv.Row(o.OrderNo, o.OrderDate, o.CustomerName, o.Mobile, o.SocialLink, o.DeliveryAddress,
                o.ProductName, o.ProductSize, o.BatchNo, o.PurchasePriceSgd, o.ProductPriceBdt, o.SellingPrice,
                o.AdvanceAmount, o.DueAmount, o.Profit, o.Status.ToText(), o.CollectedAmount, o.ReturnReason);
        }

        return File(csv.ToBytes(), "text/csv", $"orders-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var order = await orders.GetDetailsAsync(id);
        if (order is null) return NotFound();

        return View(new OrderDetailsViewModel { Order = order, History = await orders.GetHistoryAsync(id) });
    }

    /// <summary>New order: find or add the customer, then one or more products.</summary>
    [HttpGet]
    public async Task<IActionResult> Create(int? customerId) => View("Create", await orders.NewOrdersAsync(customerId));

    [HttpPost]
    public async Task<IActionResult> Create(NewOrdersInput input, string? next)
    {
        input.Items ??= [];
        if (!ModelState.IsValid)
            return View("Create", input);

        // Each product card posts its picture as Items[i].Image (the page renumbers cards before submit).
        var images = Enumerable.Range(0, input.Items.Count)
            .Select(i => Request.Form.Files.GetFile($"Items[{i}].Image").ToUploadedFile())
            .ToList();

        var result = await orders.CreateManyAsync(input, images);
        if (!Succeeded(result))
            return View("Create", input);

        var created = result.Value!;
        ToastSuccess(created.Count == 1
            ? string.Format(Messages.OrderCreated, created[0].OrderNo)
            : string.Format(Messages.OrdersCreated, created.Count, input.CustomerName.Trim()));

        return next switch
        {
            "new" => RedirectToAction(nameof(Create)),
            "same" => RedirectToAction(nameof(Create), new { customerId = created[0].CustomerId }),
            _ when created.Count == 1 => RedirectToAction(nameof(Details), new { id = created[0].Id }),
            _ => RedirectToAction("Details", "Customers", new { id = created[0].CustomerId })
        };
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var input = await orders.GetForEditAsync(id);
        return input is null ? NotFound() : View("Form", input);
    }

    [HttpPost]
    public Task<IActionResult> Edit(int id, OrderInput input, IFormFile? image)
    {
        input.Id = id;
        return SaveAsync(input, image);
    }

    // ───────── Status buttons ─────────

    [HttpPost]
    public async Task<IActionResult> Dispatch(OrderStatusInput input, string? returnUrl)
    {
        var result = await orders.DispatchAsync(input);
        return ToastAndReturn(result, string.Format(Messages.OrderDispatched, result.Value), returnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> UndoDispatch(OrderStatusInput input, string? returnUrl)
    {
        var result = await orders.UndoDispatchAsync(input);
        return ToastAndReturn(result, string.Format(Messages.OrderReverted, result.Value), returnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> Deliver(DeliverInput input, string? returnUrl)
    {
        if (!ModelState.IsValid) return InvalidAndReturn(returnUrl);

        var result = await orders.DeliverAsync(input);
        return ToastAndReturn(result, string.Format(Messages.OrderDelivered, result.Value), returnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> Return(ReturnInput input, string? returnUrl)
    {
        if (!ModelState.IsValid) return InvalidAndReturn(returnUrl);

        var result = await orders.ReturnAsync(input);
        return ToastAndReturn(result, string.Format(Messages.OrderReturned, result.Value), returnUrl);
    }

    /// <summary>New tracking link for the customer – the old one stops working (e.g. it was sent to the wrong person).</summary>
    [HttpPost]
    public async Task<IActionResult> ResetTrackingLink(int id) =>
        ToastAndReturn(await orders.ResetTrackingLinkAsync(id), Messages.TrackingLinkReset,
            Url.Action(nameof(Details), new { id }));

    [HttpPost]
    public async Task<IActionResult> Delete(int id) =>
        ToastAndReturn(await orders.DeleteAsync(id), Messages.Deleted, returnUrl: null);

    private async Task<IActionResult> SaveAsync(OrderInput input, IFormFile? image)
    {
        if (!ModelState.IsValid)
            return View("Form", input);

        var result = await orders.SaveAsync(input, image.ToUploadedFile());
        if (!Succeeded(result))
            return View("Form", input);

        var order = result.Value!;
        ToastSuccess(Messages.Updated);

        return RedirectToAction(nameof(Details), new { id = order.Id });
    }
}
