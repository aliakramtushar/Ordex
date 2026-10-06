using System.Globalization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Ordex.Core;
using Ordex.Core.Abstractions;
using Ordex.Infrastructure;
using Ordex.Infrastructure.Files;
using Ordex.Web.Infrastructure;
using Ordex.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ───────────── Services ─────────────
builder.ConfigureOrdexKestrel();

builder.Services
    .AddControllersWithViews(options =>
    {
        // Every POST/PUT/DELETE must carry a valid antiforgery token (CSRF).
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        // Currencies of the viewed company, ready before any view renders.
        options.Filters.Add<MoneyLoaderFilter>();
    });

builder.Services.AddRouting(o => o.LowercaseUrls = true);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<Money>();
builder.Services.AddScoped<MoneyLoaderFilter>();
builder.Services.AddScoped<TrackingLinks>();

builder.Services
    .AddCore()
    .AddInfrastructure(builder.Configuration)
    .AddOrdexSecurity(builder.Configuration, builder.Environment);

builder.Services.PostConfigure<FileStorageOptions>(o =>
{
    if (string.IsNullOrWhiteSpace(o.RootPath))
        o.RootPath = Path.Combine(builder.Environment.WebRootPath, "uploads");
});

// Behind IIS / Nginx / Cloudflare: trust X-Forwarded-* so rate limits see the real client IP.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

// ───────────── Pipeline (order matters) ─────────────
app.UseForwardedHeaders();
app.UseMiddleware<GlobalExceptionMiddleware>();   // 1. catch everything below
app.UseMiddleware<SecurityHeadersMiddleware>();   // 2. CSP, nosniff, frame protection

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStatusCodePagesWithReExecute("/error/{0}"); // friendly 404 / 403 / 429 pages

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Versioned assets (asp-append-version) can be cached for a long time.
        var maxAge = ctx.Context.Request.Query.ContainsKey("v") ? 31_536_000 : 604_800;
        ctx.Context.Response.Headers.CacheControl = $"public,max-age={maxAge}";
    }
});

// One fixed culture: "." for decimals and plain digits on every device/server language.
var culture = CultureInfo.GetCultureInfo("en-US");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culture),
    SupportedCultures = [culture],
    SupportedUICultures = [culture]
});

app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();                              // after auth: limits per user, else per IP
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

// Start listening FIRST so the site opens immediately, then prepare the database.
// (A slow or unreachable SQL Server must never leave the browser "loading" forever.)
await app.StartAsync();
await app.InitializeDatabaseAsync();
await app.WaitForShutdownAsync();
