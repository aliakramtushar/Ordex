namespace Ordex.Web.Middleware;

/// <summary>
/// Browser-side protection added to every response:
///  • Content-Security-Policy – only our own scripts may run (stops injected scripts / XSS),
///    the site can't be framed (clickjacking), forms can only post back to us.
///  • nosniff, referrer and permissions policies, no caching of signed-in pages.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = context.Request.IsHttps
                ? ContentSecurityPolicy + "; upgrade-insecure-requests"
                : ContentSecurityPolicy;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=(), payment=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers.Remove("Server");
            headers.Remove("X-Powered-By");

            // Pages with business data must not be stored by shared/proxy caches.
            if (context.User.Identity?.IsAuthenticated == true &&
                !context.Request.Path.StartsWithSegments("/lib") &&
                !context.Request.Path.StartsWithSegments("/css") &&
                !context.Request.Path.StartsWithSegments("/js"))
            {
                headers.CacheControl = "no-store, no-cache, must-revalidate";
                headers.Pragma = "no-cache";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}
