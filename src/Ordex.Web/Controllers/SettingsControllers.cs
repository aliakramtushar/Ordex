using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Ordex.Core.Abstractions.Services;
using Ordex.Core.Common;
using Ordex.Web.Infrastructure;
using Ordex.Web.Models;
using Ordex.Web.Theming;

namespace Ordex.Web.Controllers;

/// <summary>
/// Serves the generated theme stylesheet. The URL carries ?v={hash}, so browsers can
/// cache it for a year; a palette change produces a new hash and a fresh download.
/// </summary>
[AllowAnonymous]
public sealed class ThemeController : Controller
{
    [HttpGet("/theme.css")]
    public IActionResult Css()
    {
        var etag = new EntityTagHeaderValue($"\"{ThemeCss.Version}\"");
        if (Request.GetTypedHeaders().IfNoneMatch.Any(t => t.Compare(etag, useStrongComparison: true)))
            return StatusCode(StatusCodes.Status304NotModified);

        var headers = Response.GetTypedHeaders();
        headers.ETag = etag;
        headers.CacheControl = new CacheControlHeaderValue
        {
            Public = true,
            MaxAge = Request.Query["v"] == ThemeCss.Version ? TimeSpan.FromDays(365) : TimeSpan.FromMinutes(5)
        };
        return Content(ThemeCss.Css, "text/css; charset=utf-8");
    }
}

/// <summary>Personal settings: colour theme and light / dark mode (saved per user).</summary>
public sealed class SettingsController(IAuthService auth) : AppController
{
    [HttpGet("/settings")]
    public IActionResult Index() => View(new SettingsViewModel
    {
        Theme = UserAppearance.Theme(User),
        ColorMode = UserAppearance.Mode(User)
    });

    [HttpPost("/settings")]
    public async Task<IActionResult> Index(string theme, string colorMode)
    {
        var result = await auth.SaveAppearanceAsync(theme, colorMode);
        if (!result.Succeeded)
        {
            ToastError(result.Error ?? Messages.InvalidAppearance);
            return RedirectToAction(nameof(Index));
        }

        await UserAppearance.RefreshAsync(HttpContext, theme, colorMode);
        ToastSuccess(Messages.AppearanceSaved);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>The light/dark button in the top bar: saves the mode without leaving the page.</summary>
    [HttpPost("/settings/mode")]
    public async Task<IActionResult> Mode(string colorMode)
    {
        var theme = UserAppearance.Theme(User);
        var result = await auth.SaveAppearanceAsync(theme, colorMode);
        if (!result.Succeeded)
            return BadRequest(new { error = result.Error });

        await UserAppearance.RefreshAsync(HttpContext, theme, colorMode);
        return Json(new { ok = true, colorMode });
    }
}
