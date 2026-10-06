using System.Security.Claims;
using Ordex.Core.Abstractions;
using Ordex.Core.Abstractions.Services;

namespace Ordex.Web.Infrastructure;

/// <summary>"Company › Business unit" text for the data currently in view (mobile top bar).</summary>
public static class ScopeLabel
{
    public static async Task<string> TextAsync(ICurrentUser user, ILookupService lookups, ClaimsPrincipal principal)
    {
        var scope = user.Scope;

        var company = user.CompanyId != 0
            ? principal.FindFirst(AppClaims.CompanyName)?.Value ?? "My company"
            : scope.CompanyId == 0
                ? null
                : (await lookups.CompaniesAsync()).FirstOrDefault(c => c.Id == scope.CompanyId)?.Text;

        if (company is null)
            return "All companies";

        var unit = user.BusinessUnitId != 0
            ? principal.FindFirst(AppClaims.BusinessUnitName)?.Value
            : scope.BusinessUnitId == 0
                ? "All units"
                : (await lookups.BusinessUnitsAsync(scope.CompanyId)).FirstOrDefault(u => u.Id == scope.BusinessUnitId)?.Text ?? "All units";

        return string.IsNullOrEmpty(unit) ? company : $"{company} › {unit}";
    }
}
