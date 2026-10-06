using System.Data.Common;
using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.SqlClient;
using Ordex.Core.Common;
using Ordex.Web.Infrastructure;

namespace Ordex.Web.Middleware;

/// <summary>
/// The safety net for the whole app. Any unhandled exception is:
///   • logged once with a trace id (so support can find it),
///   • turned into ONE friendly, common message for the user,
///   • returned as JSON for fetch/AJAX calls or as the error page for normal pages.
/// Internal details (SQL, stack traces) are never shown to the user.
/// </summary>
public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public const string ErrorMessageKey = "Ordex.ErrorMessage";
    public const string TraceIdKey = "Ordex.TraceId";

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The browser went away (closed tab / lost network). Nothing to report.
            context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var (status, message) = Classify(ex);

        if (status >= 500)
            logger.LogError(ex, "Unhandled error {TraceId} on {Method} {Path}", traceId, context.Request.Method, context.Request.Path);
        else
            logger.LogWarning(ex, "Request error {TraceId} on {Method} {Path}", traceId, context.Request.Method, context.Request.Path);

        if (context.Response.HasStarted)
            throw ex is OperationCanceledException ? ex : new InvalidOperationException("Response already started.", ex);

        context.Response.Clear();
        context.Response.StatusCode = status;

        if (context.Request.WantsJson())
        {
            await context.Response.WriteAsJsonAsync(new { success = false, message, traceId });
            return;
        }

        // Re-run the pipeline for the error page (same idea as UseExceptionHandler).
        context.Items[ErrorMessageKey] = message;
        context.Items[TraceIdKey] = traceId;

        var originalPath = context.Request.Path;
        var originalMethod = context.Request.Method;
        try
        {
            context.SetEndpoint(null);
            context.Features.Get<IRouteValuesFeature>()?.RouteValues.Clear();
            context.Request.Path = $"/error/{status}";
            context.Request.Method = HttpMethods.Get;
            await next(context);
        }
        catch (Exception pageError)
        {
            // Even the error page failed – fall back to plain text.
            logger.LogError(pageError, "Error page failed for {TraceId}", traceId);
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync($"{Messages.UnexpectedError} (Ref: {traceId})");
        }
        finally
        {
            context.Request.Path = originalPath;
            context.Request.Method = originalMethod;
        }
    }

    /// <summary>Maps exception types to an HTTP status and a safe, user-facing message.</summary>
    private static (int Status, string Message) Classify(Exception ex) => ex switch
    {
        BadHttpRequestException bad => (bad.StatusCode, "The request was not valid."),
        UnauthorizedAccessException => (StatusCodes.Status403Forbidden, Messages.AccessDenied),
        TimeoutException => (StatusCodes.Status503ServiceUnavailable, "The server is busy right now. Please try again in a moment."),
        SqlException { Number: 2627 or 2601 or 547 } => (StatusCodes.Status409Conflict, "This change conflicts with existing data (duplicate or linked record)."),
        SqlException { Number: 1205 } => (StatusCodes.Status409Conflict, Messages.Conflict),
        SqlException { Number: -2 } => (StatusCodes.Status503ServiceUnavailable, "The server is busy right now. Please try again in a moment."),
        DbException => (StatusCodes.Status503ServiceUnavailable, "We couldn't reach the database. Please try again in a moment."),
        _ => (StatusCodes.Status500InternalServerError, Messages.UnexpectedError)
    };
}
