using Ordex.Core.Abstractions;
using Ordex.Core.Abstractions.Repositories;
using Ordex.Core.Abstractions.Services;
using Ordex.Core.Common;
using Ordex.Core.Entities;
using Ordex.Core.Models;

namespace Ordex.Core.Services;

public sealed class ExpenseService(
    IExpenseRepository expenses,
    IExpenseCategoryRepository categories,
    IBatchRepository batches,
    IScopeService scopeService,
    ICurrentUser user,
    IClock clock) : IExpenseService
{
    public Task<(PagedResult<ExpenseListItem> Page, decimal TotalAmount)> GetPagedAsync(ExpenseFilter filter) =>
        expenses.GetPagedAsync(user.Scope, filter);

    public Task<ExpenseInput> NewAsync() => Task.FromResult(new ExpenseInput { ExpenseDate = clock.Today });

    public async Task<ExpenseInput?> GetForEditAsync(int id)
    {
        var e = await expenses.GetByIdAsync(id, user.Scope);
        return e is null ? null : new ExpenseInput
        {
            Id = e.Id,
            CompanyId = e.CompanyId,
            BusinessUnitId = e.BusinessUnitId,
            ExpenseDate = e.ExpenseDate,
            CategoryId = e.CategoryId,
            BatchId = e.BatchId,
            Amount = e.Amount,
            Description = e.Description
        };
    }

    public async Task<ServiceResult<int>> SaveAsync(ExpenseInput input)
    {
        Expense expense;

        if (input.IsNew)
        {
            var resolved = await scopeService.ResolveForCreateAsync(input.CompanyId, input.BusinessUnitId);
            if (!resolved.Succeeded)
                return ServiceResult<int>.Fail(resolved.Error!);

            expense = new Expense
            {
                CompanyId = resolved.Value.CompanyId,
                BusinessUnitId = resolved.Value.BusinessUnitId,
                CreatedBy = user.UserId,
                CreatedAt = clock.Now
            };
        }
        else
        {
            var existing = await expenses.GetByIdAsync(input.Id, user.Scope);
            if (existing is null)
                return ServiceResult<int>.Fail(Messages.NotFound);

            expense = existing;
            expense.UpdatedBy = user.UserId;
            expense.UpdatedAt = clock.Now;
        }

        var category = await categories.GetByIdAsync(input.CategoryId.GetValueOrDefault());
        if (category is null || (category.CompanyId != 0 && category.CompanyId != expense.CompanyId))
            return ServiceResult<int>.Fail("Please select a valid category.");

        var scope = new TenantScope(expense.CompanyId, expense.BusinessUnitId);
        if (input.BatchId is { } batchId && await batches.GetByIdAsync(batchId, scope) is null)
            return ServiceResult<int>.Fail(Messages.InvalidBatch);

        expense.ExpenseDate = input.ExpenseDate.Date;
        expense.CategoryId = category.Id;
        expense.BatchId = input.BatchId;
        expense.Amount = input.Amount;
        expense.Description = TextHelper.Clean(input.Description);

        if (expense.Id == 0)
            return ServiceResult<int>.Ok(await expenses.InsertAsync(expense));

        await expenses.UpdateAsync(expense);
        return ServiceResult<int>.Ok(expense.Id);
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        if (await expenses.GetByIdAsync(id, user.Scope) is null)
            return ServiceResult.Fail(Messages.NotFound);

        await expenses.SoftDeleteAsync(id, user.UserId, clock.Now);
        return ServiceResult.Ok();
    }

    public Task<IReadOnlyList<ExpenseCategory>> GetCategoriesAsync() =>
        categories.GetListAsync(user.CompanyId, activeOnly: false);

    /// <summary>SuperAdmin creates shared categories; an Admin creates his company's own.</summary>
    public async Task<ServiceResult<int>> SaveCategoryAsync(ExpenseCategoryInput input)
    {
        if (!user.IsAdmin)
            return ServiceResult<int>.Fail(Messages.AccessDenied);

        var category = input.Id == 0
            ? new ExpenseCategory { CompanyId = user.CompanyId, BusinessUnitId = 0, CreatedBy = user.UserId, CreatedAt = clock.Now }
            : await categories.GetByIdAsync(input.Id);

        if (category is null || (category.Id != 0 && category.CompanyId != user.CompanyId && !user.IsSuperAdmin))
            return ServiceResult<int>.Fail(Messages.NotFound);

        category.CategoryName = input.CategoryName.Trim();
        category.IsActive = input.IsActive;

        if (category.Id == 0)
            return ServiceResult<int>.Ok(await categories.InsertAsync(category));

        category.UpdatedBy = user.UserId;
        category.UpdatedAt = clock.Now;
        await categories.UpdateAsync(category);
        return ServiceResult<int>.Ok(category.Id);
    }
}

public sealed class ReportService(IReportRepository reports, ICurrentUser user) : IReportService
{
    public Task<DashboardData> GetDashboardAsync(DateTime fromDate, DateTime toDate) =>
        reports.GetDashboardAsync(user.Scope, Min(fromDate, toDate), Max(fromDate, toDate));

    public Task<ProfitLossReport> GetProfitLossAsync(DateTime fromDate, DateTime toDate) =>
        reports.GetProfitLossAsync(user.Scope, Min(fromDate, toDate), Max(fromDate, toDate));

    public Task<PayableReport> GetMonthlyPayableAsync(int year) =>
        reports.GetMonthlyPayableAsync(user.Scope, Math.Clamp(year, 2000, 2100));

    private static DateTime Min(DateTime a, DateTime b) => (a <= b ? a : b).Date;
    private static DateTime Max(DateTime a, DateTime b) => (a >= b ? a : b).Date;
}
