using Microsoft.AspNetCore.Mvc;
using Ordex.Core.Abstractions;
using Ordex.Core.Abstractions.Services;
using Ordex.Web.Infrastructure;

namespace Ordex.Web.Controllers;

/// <summary>
/// The company / business-unit switcher in the top bar. SuperAdmin can pick any
/// company and unit, a company Admin any unit of his company. The choice only
/// narrows what is shown – it can never widen a user's rights.
/// </summary>
[Route("scope")]
public sealed class ScopeController(ICurrentUser user, ILookupService lookups) : AppController
{
    [HttpPost("")]
    public async Task<IActionResult> Set(int companyId, int businessUnitId, string? returnUrl)
    {
        var company = user.CompanyId != 0 ? user.CompanyId : Math.Max(companyId, 0);
        var unit = user.BusinessUnitId != 0 ? user.BusinessUnitId : Math.Max(businessUnitId, 0);

        if (company != 0 && user.CompanyId == 0 && (await lookups.CompaniesAsync()).All(c => c.Id != company))
            company = 0;

        if (company == 0)
            unit = 0;
        else if (unit != 0 && user.BusinessUnitId == 0 && (await lookups.BusinessUnitsAsync(company)).All(u => u.Id != unit))
            unit = 0;

        ViewScopeCookie.Write(Response, user.UserId, company, unit, Request.IsHttps);
        return RedirectToLocal(returnUrl, "Index", "Dashboard");
    }
}
