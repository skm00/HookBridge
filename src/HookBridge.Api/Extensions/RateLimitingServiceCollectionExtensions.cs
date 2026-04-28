using System.Security.Claims;
using System.Threading.RateLimiting;
using HookBridge.Api.RateLimiting;
using HookBridge.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using HookBridge.Shared.Api;

namespace HookBridge.Api.Extensions;

public static class RateLimitingServiceCollectionExtensions
{
    public static IServiceCollection AddHookBridgeRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("RateLimit").Get<RateLimitSettings>() ?? new RateLimitSettings();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                var httpContext = context.HttpContext;
                var loggerFactory = httpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("HookBridge.RateLimiting");

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                    httpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
                }

                var tenantId = httpContext.Request.RouteValues.TryGetValue("tenantId", out var routeTenantId)
                    ? routeTenantId?.ToString()
                    : null;

                var userId = httpContext.User.FindFirstValue("sub")
                    ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

                var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                logger.LogWarning(
                    "Rate limit exceeded. TenantId: {TenantId}, UserId: {UserId}, IPAddress: {IPAddress}, Path: {Path}",
                    tenantId,
                    userId,
                    ipAddress,
                    httpContext.Request.Path);

                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsJsonAsync(
                    ApiResponseFactory.Error(
                        "Rate limit exceeded. Please try again later.",
                        StatusCodes.Status429TooManyRequests,
                        httpContext.TraceIdentifier),
                    cancellationToken: token);
            };

            options.AddPolicy<string>(RateLimitingPolicyNames.EventIngestionPolicy, context =>
            {
                if (!settings.Enabled)
                {
                    return RateLimitPartition.GetNoLimiter("disabled");
                }

                var tenantId = context.Request.RouteValues.TryGetValue("tenantId", out var routeTenantId)
                    ? routeTenantId?.ToString()
                    : null;
                var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var partitionKey = !string.IsNullOrWhiteSpace(tenantId) ? $"tenant:{tenantId}" : $"ip:{ipAddress}";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.EventIngestionPermitLimit,
                        Window = TimeSpan.FromSeconds(settings.EventIngestionWindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            options.AddPolicy<string>(RateLimitingPolicyNames.AdminApiPolicy, context =>
            {
                if (!settings.Enabled)
                {
                    return RateLimitPartition.GetNoLimiter("disabled");
                }

                var userId = context.User.FindFirstValue("sub")
                    ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var partitionKey = !string.IsNullOrWhiteSpace(userId) ? $"user:{userId}" : $"ip:{ipAddress}";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.AdminApiPermitLimit,
                        Window = TimeSpan.FromSeconds(settings.AdminApiWindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });
        });

        return services;
    }
}
