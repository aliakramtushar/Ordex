using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Ordex.Core.Abstractions.Services;
using Ordex.Core.Common;
using Ordex.Core.Models;
using Ordex.Web.Infrastructure;
using Ordex.Web.Middleware;
using Ordex.Web.Models;

namespace Ordex.Web.Controllers;

[AllowAnonymous]
public sealed class HomeController : Controller
{
    /// <summary>Public landing page.</summary>
    [HttpGet("/")]
    public IActionResult Index() => View();
}

/// <summary>
/// Customer order tracking: /t/{token}. No sign-in and no OTP – the 128-bit random token
/// in the link is the key. Read-only: shows status and price, never cost or profit.
/// </summary>
[AllowAnonymous]
public sealed class TrackController(ITrackingService tracking) : Controller
{
    [HttpGet("/t/{token}")]
    public async Task<IActionResult> Index(string token)
    {
        var result = await tracking.GetAsync(token);

        // Personal data: never cache on shared devices or proxies, never index.
        Response.Headers.CacheControl = "no-store, max-age=0";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";

        if (result is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("NotFound");
        }

        var money = new Money();
        money.Load(new CurrencySettings(
            Currencies.Find(result.Order.PurchaseCurrency, Currencies.Sgd),
            Currencies.Find(result.Order.SalesCurrency, Currencies.Bdt),
            IsMixed: false));

        return View(new TrackViewModel { Result = result, Money = money });
    }
}

public sealed class AccountController(IAuthService auth, IConfiguration configuration) : AppController
{
    [AllowAnonymous, HttpGet("/login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? LocalRedirect(returnUrl)
                : RedirectToAction("Index", "Dashboard");

        return View(new LoginInput { ReturnUrl = returnUrl });
    }

    [AllowAnonymous, HttpPost("/login"), EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> Login(LoginInput input)
    {
        if (!ModelState.IsValid)
            return View(input);

        var result = await auth.LoginAsync(input);
        if (!Succeeded(result))
        {
            input.Password = string.Empty;
            return View(input);
        }

        ViewScopeCookie.Clear(Response);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AppClaims.BuildPrincipal(result.Value!, CookieAuthenticationDefaults.AuthenticationScheme),
            SecurityExtensions.SignInProperties(input.RememberMe, configuration));

        return !string.IsNullOrEmpty(input.ReturnUrl) && Url.IsLocalUrl(input.ReturnUrl)
            ? LocalRedirect(input.ReturnUrl)
            : RedirectToAction("Index", "Dashboard");
    }

    [HttpPost("/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        ViewScopeCookie.Clear(Response);
        return Redirect("/");
    }

    [HttpGet("/account/password")]
    public IActionResult ChangePassword() => View(new ChangePasswordInput());

    [HttpPost("/account/password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordInput input)
    {
        if (!ModelState.IsValid || !Succeeded(await auth.ChangePasswordAsync(input)))
            return View(new ChangePasswordInput());

        ToastSuccess(Messages.PasswordChanged);
        return RedirectToAction("Index", "Dashboard");
    }
}

/// <summary>Friendly pages for 403 / 404 / 429 / 500 (used by status-code pages and the exception middleware).</summary>
[AllowAnonymous]
public sealed class ErrorController : Controller
{
    [Route("/error/{code:int}")]
    public IActionResult Index(int code)
    {
        var (title, message) = code switch
        {
            403 => ("Access denied", Messages.AccessDenied),
            404 => ("Page not found", "The page you are looking for doesn't exist or was moved."),
            409 => ("Conflict", HttpContext.Items[GlobalExceptionMiddleware.ErrorMessageKey] as string ?? Messages.Conflict),
            429 => ("Please slow down", Messages.TooManyRequests),
            _ => ("Something went wrong", HttpContext.Items[GlobalExceptionMiddleware.ErrorMessageKey] as string ?? Messages.UnexpectedError)
        };

        Response.StatusCode = code is >= 400 and < 600 ? code : 500;
        ViewData["Title"] = title;
        ViewData["Message"] = message;
        ViewData["Code"] = code;
        ViewData["TraceId"] = HttpContext.Items[GlobalExceptionMiddleware.TraceIdKey] as string;
        return View("~/Views/Error/Index.cshtml");
    }
}
