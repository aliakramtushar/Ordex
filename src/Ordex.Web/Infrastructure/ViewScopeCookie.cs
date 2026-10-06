using System.Globalization;
using Ordex.Core.Common;

namespace Ordex.Web.Infrastructure;

/// <summary>
/// Remembers the company / business unit picked in the top bar.
/// Format: "userId:companyId:businessUnitId". The value is not trusted – it is
/// re-checked against the signed-in user's own scope on every request, and a
/// cookie written for another user (shared PC) is ignored.
/// </summary>
public static class ViewScopeCookie
{
    public const string Name = "ordex.view";

    public static TenantScope Resolve(HttpContext? context, int userId, TenantScope home)
    {
        if (context is null || userId == 0)
            return home;

        if (!context.Request.Cookies.TryGetValue(Name, out var raw) || !TryParse(raw, out var owner, out var companyId, out var unitId) || owner != userId)
            return home;

        var company = home.CompanyId != 0 ? home.CompanyId : companyId;
        var unit = home.BusinessUnitId != 0 ? home.BusinessUnitId : company == 0 ? 0 : unitId;
        return new TenantScope(company, unit);
    }

    public static void Write(HttpResponse response, int userId, int companyId, int businessUnitId, bool secure) =>
        response.Cookies.Append(
            Name,
            string.Create(CultureInfo.InvariantCulture, $"{userId}:{companyId}:{businessUnitId}"),
            new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                MaxAge = TimeSpan.FromDays(180),
                Path = "/"
            });

    public static void Clear(HttpResponse response) => response.Cookies.Delete(Name, new CookieOptions { Path = "/" });

    private static bool TryParse(string? raw, out int userId, out int companyId, out int unitId)
    {
        userId = companyId = unitId = 0;
        var parts = raw?.Split(':');
        return parts is { Length: 3 } &&
               int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out userId) &&
               int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out companyId) &&
               int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out unitId);
    }
}
