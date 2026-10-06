using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Ordex.Core.Abstractions;
using Ordex.Core.Abstractions.Services;
using Ordex.Core.Common;
using Ordex.Core.Enums;

namespace Ordex.Web.Infrastructure;

/// <summary>Claim names stored in the auth cookie.</summary>
public static class AppClaims
{
    public const string CompanyId = "ordex:company_id";
    public const string BusinessUnitId = "ordex:bu_id";
    public const string CompanyName = "ordex:company_name";
    public const string BusinessUnitName = "ordex:bu_name";
    public const string UserName = "ordex:username";
    public const string Theme = "ordex:theme";
    public const string ColorMode = "ordex:mode";

    public static ClaimsPrincipal BuildPrincipal(AuthenticatedUser user, string scheme)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(UserName, user.UserName),
            new(CompanyId, user.CompanyId.ToString(CultureInfo.InvariantCulture)),
            new(BusinessUnitId, user.BusinessUnitId.ToString(CultureInfo.InvariantCulture)),
            new(CompanyName, user.CompanyName),
            new(BusinessUnitName, user.BusinessUnitName),
            new(Theme, Appearance.NormalizeTheme(user.Theme)),
            new(ColorMode, Appearance.NormalizeMode(user.ColorMode))
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, scheme));
    }
}

/// <summary>Reads the signed-in user from the request's cookie claims.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public int UserId => IntClaim(ClaimTypes.NameIdentifier);
    public string UserName => Principal?.FindFirstValue(AppClaims.UserName) ?? string.Empty;
    public string FullName => Principal?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
    public int CompanyId => IntClaim(AppClaims.CompanyId);
    public int BusinessUnitId => IntClaim(AppClaims.BusinessUnitId);

    public UserRole Role =>
        Enum.TryParse<UserRole>(Principal?.FindFirstValue(ClaimTypes.Role), out var role) ? role : UserRole.Staff;

    public TenantScope HomeScope => new(CompanyId, BusinessUnitId);

    private TenantScope? _scope;

    /// <summary>
    /// Home scope narrowed by the top-bar switcher cookie. The cookie can only ever
    /// narrow what the user may see: values outside the user's rights are ignored.
    /// </summary>
    public TenantScope Scope => _scope ??= ViewScopeCookie.Resolve(accessor.HttpContext, UserId, HomeScope);

    private int IntClaim(string type) =>
        int.TryParse(Principal?.FindFirstValue(type), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
}

/// <summary>The signed-in user's theme and light/dark mode, read from the cookie (no database call per page).</summary>
public static class UserAppearance
{
    public static string Theme(ClaimsPrincipal? user) =>
        Appearance.NormalizeTheme(user?.FindFirstValue(AppClaims.Theme));

    public static string Mode(ClaimsPrincipal? user) =>
        Appearance.NormalizeMode(user?.FindFirstValue(AppClaims.ColorMode));

    /// <summary>
    /// Re-issues the sign-in cookie with the new look, keeping every other claim and the
    /// original cookie settings (remember-me, expiry) – so the change shows on the next page at once.
    /// </summary>
    public static async Task RefreshAsync(HttpContext http, string theme, string mode)
    {
        var auth = await http.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!auth.Succeeded || auth.Principal?.Identity is not ClaimsIdentity identity)
            return;

        var claims = identity.Claims
            .Where(c => c.Type != AppClaims.Theme && c.Type != AppClaims.ColorMode)
            .Append(new Claim(AppClaims.Theme, theme))
            .Append(new Claim(AppClaims.ColorMode, mode));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, identity.AuthenticationType, identity.NameClaimType, identity.RoleClaimType));
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, auth.Properties);
    }
}
