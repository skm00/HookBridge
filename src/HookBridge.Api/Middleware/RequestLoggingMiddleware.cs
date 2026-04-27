using System.Diagnostics;
using System.Security.Claims;
using Serilog.Context;

namespace HookBridge.Api.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = ResolveCorrelationId(context);
        var tenantId = ResolveTenantId(context);

        using (PushTenantId(tenantId))
        {
            await next(context);
        }

        stopwatch.Stop();

        logger.LogInformation(
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms (correlationId: {CorrelationId}, tenantId: {TenantId})",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            correlationId,
            tenantId);
    }

    private static IDisposable PushTenantId(string? tenantId)
    {
        return string.IsNullOrWhiteSpace(tenantId)
            ? LogContext.PushProperty("tenantId", null)
            : LogContext.PushProperty("tenantId", tenantId);
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var itemValue)
            && itemValue is string correlationId
            && !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        return context.TraceIdentifier;
    }

    private static string? ResolveTenantId(HttpContext context)
    {
        if (context.Request.RouteValues.TryGetValue("tenantId", out var routeTenantId)
            && routeTenantId is not null)
        {
            return routeTenantId.ToString();
        }

        return context.User.FindFirstValue("tenantId") ?? context.User.FindFirstValue("tenant_id");
    }
}
