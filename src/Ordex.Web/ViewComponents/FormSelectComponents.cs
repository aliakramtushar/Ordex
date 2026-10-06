using Microsoft.AspNetCore.Mvc;
using Ordex.Core.Abstractions;
using Ordex.Core.Abstractions.Services;
using Ordex.Core.Models;

namespace Ordex.Web.ViewComponents;

/*  Reusable form widgets. Any form can drop these in with one line, and the
    dropdown data always respects the signed-in user's company / business unit. */

public sealed class ScopeFieldsModel
{
    public bool ShowCompany { get; init; }
    public bool ShowUnit { get; init; }
    public bool AllowAllUnits { get; init; }
    public int? CompanyId { get; init; }
    public int? BusinessUnitId { get; init; }
    public IReadOnlyList<LookupItem> Companies { get; init; } = [];
    public IReadOnlyList<LookupItem> BusinessUnits { get; init; } = [];
}

/// <summary>
/// Company + business-unit pickers. Rendered only for users who can see more
/// than one (SuperAdmin / company Admin); everyone else writes to their own unit.
/// </summary>
public sealed class ScopeFieldsViewComponent(ICurrentUser user, ILookupService lookups) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int? companyId, int? businessUnitId, bool allowAllUnits = false, bool showUnit = true)
    {
        var showCompany = user.CompanyId == 0;
        var showUnitPicker = showUnit && user.BusinessUnitId == 0;

        if (!showCompany && !showUnitPicker)
            return Content(string.Empty);

        // New record with nothing chosen yet: start from the company / unit picked in the top bar.
        // (Not for user accounts, where 0 = "all" is a real choice.)
        if (!allowAllUnits && companyId is null or 0 && user.Scope.CompanyId != 0)
        {
            companyId = user.Scope.CompanyId;
            if (businessUnitId is null or 0)
                businessUnitId = user.Scope.BusinessUnitId == 0 ? null : user.Scope.BusinessUnitId;
        }

        var effectiveCompany = user.CompanyId != 0 ? user.CompanyId : companyId;

        return View(new ScopeFieldsModel
        {
            ShowCompany = showCompany,
            ShowUnit = showUnitPicker,
            AllowAllUnits = allowAllUnits,
            CompanyId = companyId,
            BusinessUnitId = businessUnitId,
            Companies = showCompany ? await lookups.CompaniesAsync() : [],
            BusinessUnits = effectiveCompany is > 0 ? await lookups.BusinessUnitsAsync(effectiveCompany) : []
        });
    }
}

public sealed class BatchSelectModel
{
    public string Name { get; init; } = "BatchId";
    public int? Selected { get; init; }
    public bool DependsOnUnit { get; init; }
    public IReadOnlyList<BatchLookupItem> Batches { get; init; } = [];
}

/// <summary>Purchase batch dropdown. Each option carries its exchange rate (data-rate) for the BDT auto-calc.</summary>
public sealed class BatchSelectViewComponent(ICurrentUser user, ILookupService lookups) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int? selected, int? companyId, int? businessUnitId, string name = "BatchId")
    {
        var batches = await lookups.BatchesAsync(companyId, businessUnitId);
        return View(new BatchSelectModel
        {
            Name = name,
            Selected = selected,
            DependsOnUnit = user.BusinessUnitId == 0,
            Batches = batches
        });
    }
}

public sealed class CategorySelectModel
{
    public int? Selected { get; init; }
    public IReadOnlyList<LookupItem> Categories { get; init; } = [];
}

public sealed class CategorySelectViewComponent(ILookupService lookups) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int? selected, int? companyId) =>
        View(new CategorySelectModel { Selected = selected, Categories = await lookups.ExpenseCategoriesAsync(companyId) });
}

public sealed class ScopeSwitcherModel
{
    public bool CanPickCompany { get; init; }
    public bool CanPickUnit { get; init; }
    public int CompanyId { get; init; }
    public int BusinessUnitId { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string UnitName { get; init; } = string.Empty;
    public string ReturnUrl { get; init; } = "/dashboard";
    public IReadOnlyList<LookupItem> Companies { get; init; } = [];
    public IReadOnlyList<LookupItem> BusinessUnits { get; init; } = [];

    /// <summary>"desktop" = inline in the top bar, "sheet" = stacked in the mobile sheet.</summary>
    public string Variant { get; init; } = "desktop";

    public bool IsInteractive => CanPickCompany || CanPickUnit;
}

/// <summary>
/// Company + business unit switcher in the top bar. Users bound to a single unit
/// just see where they are; SuperAdmin / company Admin get dropdowns that filter
/// every page (see <see cref="Infrastructure.ViewScopeCookie"/>).
/// </summary>
public sealed class ScopeSwitcherViewComponent(ICurrentUser user, ILookupService lookups) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(string variant = "desktop")
    {
        var scope = user.Scope;
        var canPickCompany = user.CompanyId == 0;
        var canPickUnit = user.BusinessUnitId == 0;

        IReadOnlyList<LookupItem> companies = canPickCompany ? await lookups.CompaniesAsync() : Array.Empty<LookupItem>();
        IReadOnlyList<LookupItem> units = canPickUnit && scope.CompanyId != 0 ? await lookups.BusinessUnitsAsync(scope.CompanyId) : Array.Empty<LookupItem>();

        var companyName = canPickCompany
            ? companies.FirstOrDefault(c => c.Id == scope.CompanyId)?.Text ?? "All companies"
            : UserClaimsPrincipal.FindFirst(Infrastructure.AppClaims.CompanyName)?.Value ?? "My company";
        var unitName = canPickUnit
            ? units.FirstOrDefault(u => u.Id == scope.BusinessUnitId)?.Text ?? "All business units"
            : UserClaimsPrincipal.FindFirst(Infrastructure.AppClaims.BusinessUnitName)?.Value ?? "My unit";

        return View(new ScopeSwitcherModel
        {
            CanPickCompany = canPickCompany,
            CanPickUnit = canPickUnit,
            CompanyId = scope.CompanyId,
            BusinessUnitId = scope.BusinessUnitId,
            CompanyName = companyName,
            UnitName = unitName,
            Companies = companies,
            BusinessUnits = units,
            ReturnUrl = ReturnUrlAfterSwitch(),
            Variant = variant
        });
    }

    /// <summary>
    /// After switching, stay on the same list/report – but leave a single record
    /// (it may not belong to the new company) and drop the page number.
    /// </summary>
    private string ReturnUrlAfterSwitch()
    {
        var route = ViewContext.RouteData.Values;
        var controller = route["controller"]?.ToString();
        if (route.ContainsKey("id") && controller is not null)
            return Url.Action("Index", controller) ?? "/dashboard";

        var request = HttpContext.Request;
        var query = request.Query.Where(q => !string.Equals(q.Key, "page", StringComparison.OrdinalIgnoreCase)).ToList();
        var qs = query.Count == 0 ? string.Empty : QueryString.Create(query).ToString();
        return $"{request.PathBase}{request.Path}{qs}";
    }
}
