using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Ordex.Core.Abstractions.Services;
using Ordex.Core.Common;
using Ordex.Core.Enums;

namespace Ordex.Web.Infrastructure;

public static class AppRoles
{
    public const string SuperAdmin = nameof(UserRole.SuperAdmin);
    public const string Admins = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.Admin)}";
}

public static class RateLimitPolicies
{
    public const string Login = "login";
}

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public int RequestsPerMinutePerClient { get; set; } = 240;
    public int LoginAttemptsPerFiveMinutes { get; set; } = 10;
    public int MaxRequestBodySizeMb { get; set; } = 12;
    public int SessionHours { get; set; } = 10;
    public int RememberMeDays { get; set; } = 14;
}

/// <summary>
/// Everything that protects the app, in one place:
/// cookie auth, antiforgery (CSRF), request size limits, rate limiting (DoS / brute force)
/// and server timeouts (slow-loris).
/// </summary>
public static class SecurityExtensions
{
    private const string LastValidatedKey = "ordex:validated";
    private static readonly TimeSpan RevalidateEvery = TimeSpan.FromMinutes(5);

    public static IServiceCollection AddOrdexSecurity(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment env)
    {
        var security = configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>() ?? new SecurityOptions();
        var maxBodyBytes = security.MaxRequestBodySizeMb * 1024L * 1024L;

        // ── Keys that encrypt cookies survive app restarts (users stay signed in) ──
        services.AddDataProtection()
            .SetApplicationName("Ordex")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(env.ContentRootPath, "App_Data", "keys")));

        // ── Authentication (cookie) ──
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/login";
                options.LogoutPath = "/logout";
                options.AccessDeniedPath = "/error/403";
                options.Cookie.Name = "ordex.auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = env.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromHours(security.SessionHours);
                options.SlidingExpiration = true;
                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = ValidateSessionAsync,
                    OnRedirectToLogin = ctx => JsonOrRedirect(ctx, StatusCodes.Status401Unauthorized),
                    OnRedirectToAccessDenied = ctx => JsonOrRedirect(ctx, StatusCodes.Status403Forbidden)
                };
            });

        services.AddAuthorizationBuilder()
            // Secure by default: every page needs a login unless marked [AllowAnonymous].
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        // ── CSRF protection ──
        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "ordex.af";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = env.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.HeaderName = "RequestVerificationToken";
        });

        // ── Request size limits (big uploads / form floods) ──
        services.Configure<FormOptions>(o =>
        {
            o.MultipartBodyLengthLimit = maxBodyBytes;
            o.ValueCountLimit = 512;
            o.ValueLengthLimit = 64 * 1024;
            o.KeyLengthLimit = 256;
        });
        // IIS: the same limit is set in web.config (requestFiltering / maxAllowedContentLength).

        // ── Rate limiting: per signed-in user, or per IP for visitors ──
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = OnRejectedAsync;

            var perMinute = Math.Max(30, security.RequestsPerMinutePerClient);
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                RateLimitPartition.GetTokenBucketLimiter(ClientKey(ctx), _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = perMinute,
                    TokensPerPeriod = Math.Max(1, perMinute / 6),
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

            // Brute-force protection for the login form (on top of account lockout).
            options.AddPolicy(RateLimitPolicies.Login, ctx =>
                RateLimitPartition.GetFixedWindowLimiter($"login:{ctx.Connection.RemoteIpAddress}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(3, security.LoginAttemptsPerFiveMinutes),
                    Window = TimeSpan.FromMinutes(5),
                    QueueLimit = 0
                }));
        });

        services.AddHsts(o =>
        {
            o.MaxAge = TimeSpan.FromDays(365);
            o.IncludeSubDomains = true;
        });

        return services;
    }

    /// <summary>Kestrel hardening: body size, header limits and timeouts against slow clients.</summary>
    public static WebApplicationBuilder ConfigureOrdexKestrel(this WebApplicationBuilder builder)
    {
        var security = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>() ?? new SecurityOptions();

        builder.WebHost.ConfigureKestrel(k =>
        {
            k.AddServerHeader = false;
            k.Limits.MaxRequestBodySize = security.MaxRequestBodySizeMb * 1024L * 1024L;
            k.Limits.MaxRequestHeaderCount = 60;
            k.Limits.MaxRequestHeadersTotalSize = 32 * 1024;
            k.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(20);
            k.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
            k.Limits.MinRequestBodyDataRate = new Microsoft.AspNetCore.Server.Kestrel.Core.MinDataRate(240, TimeSpan.FromSeconds(10));
        });

        return builder;
    }

    public static AuthenticationProperties SignInProperties(bool rememberMe, IConfiguration configuration)
    {
        var security = configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>() ?? new SecurityOptions();
        var props = new AuthenticationProperties { IsPersistent = rememberMe, AllowRefresh = true };
        if (rememberMe)
            props.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(security.RememberMeDays);
        props.Items[LastValidatedKey] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        return props;
    }

    /// <summary>
    /// Every few minutes, re-check the user in the database. A deactivated user
    /// (or one moved to another company/unit) is signed out right away.
    /// </summary>
    private static async Task ValidateSessionAsync(CookieValidatePrincipalContext ctx)
    {
        var now = DateTimeOffset.UtcNow;
        if (ctx.Properties.Items.TryGetValue(LastValidatedKey, out var stamp) &&
            DateTimeOffset.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var last) &&
            now - last < RevalidateEvery)
            return;

        var principal = ctx.Principal;
        var userId = IntClaim(principal, ClaimTypes.NameIdentifier);
        var valid = userId > 0 &&
                    Enum.TryParse<UserRole>(principal?.FindFirstValue(ClaimTypes.Role), out var role) &&
                    await ctx.HttpContext.RequestServices.GetRequiredService<IAuthService>()
                        .IsSessionValidAsync(userId, role, IntClaim(principal, AppClaims.CompanyId), IntClaim(principal, AppClaims.BusinessUnitId));

        if (!valid)
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        ctx.Properties.Items[LastValidatedKey] = now.ToString("O", CultureInfo.InvariantCulture);
        ctx.ShouldRenew = true;
    }

    private static Task JsonOrRedirect(RedirectContext<CookieAuthenticationOptions> ctx, int status)
    {
        if (ctx.Request.WantsJson())
        {
            ctx.Response.StatusCode = status;
            return ctx.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = status == StatusCodes.Status401Unauthorized ? "Your session has expired. Please sign in again." : Messages.AccessDenied
            });
        }

        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    }

    private static async ValueTask OnRejectedAsync(OnRejectedContext ctx, CancellationToken ct)
    {
        var http = ctx.HttpContext;
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            http.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

        http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RateLimit")
            .LogWarning("Rate limit hit by {Client} on {Path}", ClientKey(http), http.Request.Path);

        if (http.Request.WantsJson())
        {
            await http.Response.WriteAsJsonAsync(new { success = false, message = Messages.TooManyRequests }, ct);
            return;
        }

        http.Response.ContentType = "text/html; charset=utf-8";
        await http.Response.WriteAsync($$"""
            <!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>Slow down · Ordex</title></head>
            <body style="font-family:system-ui,sans-serif;background:#f6f5f1;color:#22252a;display:grid;place-items:center;min-height:100vh;margin:0;padding:16px">
            <div style="max-width:420px;text-align:center"><h1 style="font-size:1.4rem">Please slow down</h1>
            <p style="color:#6b6f76">{{Messages.TooManyRequests}}</p><a href="/" style="color:#2f6b5f">Back to Ordex</a></div></body></html>
            """, ct);
    }

    /// <summary>Signed-in users are limited per account, visitors per IP address.</summary>
    private static string ClientKey(HttpContext ctx) =>
        ctx.User.Identity?.IsAuthenticated == true
            ? $"user:{ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)}"
            : $"ip:{ctx.Connection.RemoteIpAddress}";

    private static int IntClaim(ClaimsPrincipal? principal, string type) =>
        int.TryParse(principal?.FindFirstValue(type), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
}
