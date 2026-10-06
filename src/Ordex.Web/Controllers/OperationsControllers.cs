using Microsoft.AspNetCore.Mvc;
using Ordex.Core.Abstractions;
using Ordex.Core.Abstractions.Services;
using Ordex.Core.Common;
using Ordex.Core.Models;
using Ordex.Web.Infrastructure;
using Ordex.Web.Models;

namespace Ordex.Web.Controllers;

public sealed class DashboardController(IReportService reports, IClock clock) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        var range = DateRange.Resolve(from, to, clock.Today);
        var data = await reports.GetDashboardAsync(range.From, range.To);
        return View(new DashboardViewModel { Data = data, Range = range });
    }
}

public sealed class StockController(IStockService stock) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(StockFilter filter) =>
        View(new StockIndexViewModel
        {
            Filter = filter,
            Result = await stock.GetPagedAsync(filter),
            Counts = await stock.GetTabCountsAsync()
        });

    [HttpGet]
    public async Task<IActionResult> Create() => View("Form", await stock.NewAsync());

    [HttpPost]
    public Task<IActionResult> Create(StockInput input, IFormFile? image) => SaveAsync(input, image);

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var input = await stock.GetForEditAsync(id);
        return input is null ? NotFound() : View("Form", input);
    }

    [HttpPost]
    public Task<IActionResult> Edit(int id, StockInput input, IFormFile? image)
    {
        input.Id = id;
        return SaveAsync(input, image);
    }

    [HttpPost]
    public async Task<IActionResult> Sell(StockSaleInput input, string? returnUrl)
    {
        if (!ModelState.IsValid) return InvalidAndReturn(returnUrl);
        return ToastAndReturn(await stock.SellAsync(input), Messages.StockSold, returnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, string? returnUrl) =>
        ToastAndReturn(await stock.DeleteAsync(id), Messages.Deleted, returnUrl);

    private async Task<IActionResult> SaveAsync(StockInput input, IFormFile? image)
    {
        if (!ModelState.IsValid || !Succeeded(await stock.SaveExtraAsync(input, image.ToUploadedFile())))
            return View("Form", input);

        ToastSuccess(input.IsNew ? Messages.Saved : Messages.Updated);
        return RedirectToAction(nameof(Index), new { tab = StockTab.Extra });
    }
}

public sealed class BatchesController(IBatchService batches, IOrderService orders) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(BatchFilter filter) =>
        View(new BatchIndexViewModel { Filter = filter, Result = await batches.GetPagedAsync(filter) });

    [HttpGet]
    public async Task<IActionResult> Details(int id, int page = 1)
    {
        var batch = await batches.GetTotalsAsync(id);
        if (batch is null) return NotFound();

        var batchOrders = await orders.GetPagedAsync(new OrderFilter { BatchId = id, Page = page, PageSize = 30 });
        return View(new BatchDetailsViewModel { Batch = batch, Orders = batchOrders });
    }

    [HttpGet]
    public async Task<IActionResult> Create() => View("Form", await batches.NewAsync());

    [HttpPost]
    public Task<IActionResult> Create(BatchInput input) => SaveAsync(input);

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var input = await batches.GetForEditAsync(id);
        return input is null ? NotFound() : View("Form", input);
    }

    [HttpPost]
    public Task<IActionResult> Edit(int id, BatchInput input)
    {
        input.Id = id;
        return SaveAsync(input);
    }

    [HttpPost]
    public async Task<IActionResult> SetPaid(int id, bool isPaid, string? returnUrl) =>
        ToastAndReturn(await batches.SetPaidAsync(id, isPaid),
            isPaid ? "Batch marked as paid." : "Batch marked as unpaid.", returnUrl);

    private async Task<IActionResult> SaveAsync(BatchInput input)
    {
        if (!ModelState.IsValid)
            return View("Form", input);

        var result = await batches.SaveAsync(input);
        if (!Succeeded(result))
            return View("Form", input);

        ToastSuccess(input.IsNew ? Messages.Saved : Messages.Updated);
        return RedirectToAction(nameof(Details), new { id = result.Value });
    }
}

public sealed class ExpensesController(IExpenseService expenses, ILookupService lookups) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(ExpenseFilter filter)
    {
        var (page, total) = await expenses.GetPagedAsync(filter);
        return View(new ExpenseIndexViewModel
        {
            Filter = filter,
            Result = page,
            TotalAmount = total,
            Categories = await lookups.ExpenseCategoriesAsync(null)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create() => View("Form", await expenses.NewAsync());

    [HttpPost]
    public Task<IActionResult> Create(ExpenseInput input) => SaveAsync(input);

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var input = await expenses.GetForEditAsync(id);
        return input is null ? NotFound() : View("Form", input);
    }

    [HttpPost]
    public Task<IActionResult> Edit(int id, ExpenseInput input)
    {
        input.Id = id;
        return SaveAsync(input);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, string? returnUrl) =>
        ToastAndReturn(await expenses.DeleteAsync(id), Messages.Deleted, returnUrl);

    [HttpGet]
    public async Task<IActionResult> Categories() =>
        View(new CategoriesViewModel { Categories = await expenses.GetCategoriesAsync() });

    [HttpPost]
    public async Task<IActionResult> SaveCategory(ExpenseCategoryInput input)
    {
        if (!ModelState.IsValid) return InvalidAndReturn(null, nameof(Categories));
        return ToastAndReturn(await expenses.SaveCategoryAsync(input), Messages.Saved, null, nameof(Categories));
    }

    private async Task<IActionResult> SaveAsync(ExpenseInput input)
    {
        if (!ModelState.IsValid || !Succeeded(await expenses.SaveAsync(input)))
            return View("Form", input);

        ToastSuccess(input.IsNew ? Messages.Saved : Messages.Updated);
        return RedirectToAction(nameof(Index));
    }
}

public sealed class CustomersController(ICustomerService customers, IOrderService orders) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(CustomerFilter filter) =>
        View(new CustomerIndexViewModel { Filter = filter, Result = await customers.GetPagedAsync(filter) });

    [HttpGet]
    public async Task<IActionResult> Details(int id, int page = 1)
    {
        var customer = await customers.GetAsync(id);
        if (customer is null) return NotFound();

        var history = await orders.GetPagedAsync(new OrderFilter { CustomerId = id, Page = page, PageSize = 20 });
        return View(new CustomerDetailsViewModel { Customer = customer, Orders = history });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var input = await customers.GetForEditAsync(id);
        return input is null ? NotFound() : View(input);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, CustomerInput input)
    {
        input.Id = id;
        if (!ModelState.IsValid || !Succeeded(await customers.UpdateAsync(input)))
            return View(input);

        ToastSuccess(Messages.Updated);
        return RedirectToAction(nameof(Details), new { id });
    }
}

public sealed class ReportsController(IReportService reports, IClock clock) : AppController
{
    [HttpGet]
    public async Task<IActionResult> ProfitLoss(DateTime? from, DateTime? to)
    {
        var range = DateRange.Resolve(from, to, clock.Today);
        return View(new ProfitLossViewModel { Report = await reports.GetProfitLossAsync(range.From, range.To), Range = range });
    }

    [HttpGet]
    public async Task<IActionResult> Payable(int? year) =>
        View(await reports.GetMonthlyPayableAsync(year ?? clock.Today.Year));
}

/// <summary>Small JSON endpoints for dependent dropdowns and customer auto-fill.</summary>
[Route("lookup")]
public sealed class LookupController(ILookupService lookups, IBatchService batches) : AppController
{
    [HttpGet("business-units")]
    public async Task<IActionResult> BusinessUnits(int? companyId) =>
        Json((await lookups.BusinessUnitsAsync(companyId)).Select(u => new { value = u.Id, text = u.Text }));

    [HttpGet("batches")]
    public async Task<IActionResult> Batches(int? companyId, int? businessUnitId) =>
        Json((await lookups.BatchesAsync(companyId, businessUnitId)).Select(b => new
        {
            value = b.Id,
            text = $"{b.BatchNo} · {Fmt.Date(b.BatchDate)}{(b.IsPaid ? " · paid" : string.Empty)}",
            rate = b.ExchangeRate
        }));

    [HttpGet("customers")]
    public async Task<IActionResult> Customers(string? term, int? companyId, int? businessUnitId) =>
        Json((await lookups.SearchCustomersAsync(term ?? string.Empty, companyId, businessUnitId)).Select(c => new
        {
            id = c.Id,
            name = c.CustomerName,
            mobile = c.Mobile,
            socialLink = c.SocialLink,
            address = c.Address,
            companyId = c.CompanyId,
            businessUnitId = c.BusinessUnitId,
            unit = c.BusinessUnitName,
            orders = c.OrderCount,
            lastOrder = c.LastOrderDate is { } d ? Fmt.Date(d) : null
        }));

    /// <summary>"New batch" sheet on the order screen: create it and hand back the dropdown option.</summary>
    [HttpPost("batches")]
    public async Task<IActionResult> CreateBatch([FromForm] BatchInput input)
    {
        input.Id = 0;
        if (!ModelState.IsValid)
            return BadRequest(new { error = FirstModelError() });

        var result = await batches.SaveAsync(input);
        if (!result.Succeeded)
            return BadRequest(new { error = result.Error });

        var saved = await batches.GetForEditAsync(result.Value);
        return Json(new
        {
            value = result.Value,
            text = $"{saved?.BatchNo} · {Fmt.Date(input.BatchDate)}",
            rate = input.ExchangeRate
        });
    }

    [HttpGet("rate")]
    public async Task<IActionResult> Rate(int? companyId) =>
        Json(new { rate = await lookups.DefaultExchangeRateAsync(companyId) });
}
