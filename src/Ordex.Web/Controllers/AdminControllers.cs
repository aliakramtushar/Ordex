using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ordex.Core.Abstractions;
using Ordex.Core.Abstractions.Services;
using Ordex.Core.Common;
using Ordex.Core.Models;
using Ordex.Web.Infrastructure;
using Ordex.Web.Models;

namespace Ordex.Web.Controllers;

/// <summary>
/// SuperAdmin: full company list with add / edit / on-off / delete.
/// Admin: only "Company profile" – edit his own company's details.
/// </summary>
[Authorize(Roles = AppRoles.Admins)]
public sealed class CompaniesController(ICompanyService companies, ICurrentUser currentUser) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index() =>
        currentUser.IsSuperAdmin ? View(await companies.GetAllAsync()) : RedirectToAction(nameof(Profile));

    [HttpGet, Authorize(Roles = AppRoles.SuperAdmin)]
    public IActionResult Create() => View("Form", new CompanyInput());

    [HttpPost, Authorize(Roles = AppRoles.SuperAdmin)]
    public Task<IActionResult> Create(CompanyInput input) => SaveAsync(input);

    [HttpGet, Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Edit(int id)
    {
        var input = await companies.GetForEditAsync(id);
        return input is null ? NotFound() : View("Form", input);
    }

    [HttpPost, Authorize(Roles = AppRoles.SuperAdmin)]
    public Task<IActionResult> Edit(int id, CompanyInput input)
    {
        input.Id = id;
        return SaveAsync(input);
    }

    [HttpPost, Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> SetActive(int id, bool active, string? returnUrl)
    {
        var result = await companies.SetActiveAsync(id, active);
        return ToastAndReturn(result, string.Format(active ? Messages.Activated : Messages.Deactivated, result.Value), returnUrl);
    }

    [HttpPost, Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Delete(int id) =>
        ToastAndReturn(await companies.DeleteAsync(id), Messages.Deleted, returnUrl: null);

    // ───────── Admin: own company ─────────

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        if (currentUser.IsSuperAdmin) return RedirectToAction(nameof(Index));
        var input = await companies.GetProfileAsync();
        return input is null ? NotFound() : View(input);
    }

    [HttpPost]
    public async Task<IActionResult> Profile(CompanyProfileInput input)
    {
        if (currentUser.IsSuperAdmin) return RedirectToAction(nameof(Index));

        if (!ModelState.IsValid || !Succeeded(await companies.SaveProfileAsync(input)))
        {
            input.CompanyCode = (await companies.GetProfileAsync())?.CompanyCode ?? string.Empty;
            return View(input);
        }

        ToastSuccess(Messages.Updated);
        return RedirectToAction(nameof(Profile));
    }

    private async Task<IActionResult> SaveAsync(CompanyInput input)
    {
        if (!ModelState.IsValid || !Succeeded(await companies.SaveAsync(input)))
            return View("Form", input);

        ToastSuccess(input.Id == 0 ? Messages.Saved : Messages.Updated);
        return RedirectToAction(nameof(Index));
    }
}

/// <summary>
/// SuperAdmin: add / edit / on-off / delete units of any company.
/// Admin: edit the units he can see – no add, delete or on-off.
/// </summary>
[Authorize(Roles = AppRoles.Admins)]
public sealed class BusinessUnitsController(IBusinessUnitService units) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index() => View(await units.GetListAsync());

    [HttpGet, Authorize(Roles = AppRoles.SuperAdmin)]
    public IActionResult Create(int? companyId) => View("Form", new BusinessUnitInput { CompanyId = companyId });

    [HttpPost, Authorize(Roles = AppRoles.SuperAdmin)]
    public Task<IActionResult> Create(BusinessUnitInput input) => SaveAsync(input);

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var input = await units.GetForEditAsync(id);
        return input is null ? NotFound() : View("Form", input);
    }

    [HttpPost]
    public Task<IActionResult> Edit(int id, BusinessUnitInput input)
    {
        input.Id = id;
        return SaveAsync(input);
    }

    [HttpPost, Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> SetActive(int id, bool active, string? returnUrl)
    {
        var result = await units.SetActiveAsync(id, active);
        return ToastAndReturn(result, string.Format(active ? Messages.Activated : Messages.Deactivated, result.Value), returnUrl);
    }

    [HttpPost, Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Delete(int id) =>
        ToastAndReturn(await units.DeleteAsync(id), Messages.Deleted, returnUrl: null);

    private async Task<IActionResult> SaveAsync(BusinessUnitInput input)
    {
        if (!ModelState.IsValid || !Succeeded(await units.SaveAsync(input)))
            return View("Form", input);

        ToastSuccess(input.Id == 0 ? Messages.Saved : Messages.Updated);
        return RedirectToAction(nameof(Index));
    }
}

[Authorize(Roles = AppRoles.Admins)]
public sealed class UsersController(IUserService users) : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index(UserFilter filter) =>
        View(new UserIndexViewModel { Result = await users.GetPagedAsync(filter), Filter = filter });

    [HttpGet]
    public IActionResult Create() => View("Form", new UserInput());

    [HttpPost]
    public Task<IActionResult> Create(UserInput input) => SaveAsync(input);

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var input = await users.GetForEditAsync(id);
        return input is null ? NotFound() : View("Form", input);
    }

    [HttpPost]
    public Task<IActionResult> Edit(int id, UserInput input)
    {
        input.Id = id;
        return SaveAsync(input);
    }

    private async Task<IActionResult> SaveAsync(UserInput input)
    {
        if (!ModelState.IsValid || !Succeeded(await users.SaveAsync(input)))
        {
            input.Password = null;
            return View("Form", input);
        }

        ToastSuccess(input.Id == 0 ? Messages.Saved : Messages.Updated);
        return RedirectToAction(nameof(Index));
    }
}
