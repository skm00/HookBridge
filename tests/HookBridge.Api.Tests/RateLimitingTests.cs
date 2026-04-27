using System.Net;
using System.Security.Claims;
using System.Text;
using HookBridge.Api.Extensions;
using HookBridge.Api.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HookBridge.Api.Tests;

public sealed class RateLimitingTests
{
    [Fact]
    public async Task EventIngestionRateLimit_AppliesPerTenantId()
    {
        using var server = BuildServer(new Dictionary<string, string?>
        {
            ["RateLimit:Enabled"] = "true",
            ["RateLimit:EventIngestionPermitLimit"] = "1",
            ["RateLimit:EventIngestionWindowSeconds"] = "60",
            ["RateLimit:AdminApiPermitLimit"] = "5",
            ["RateLimit:AdminApiWindowSeconds"] = "60",
        });

        using var client = server.CreateClient();

        var firstTenant1 = await client.PostAsync("/api/v1/events/tenant-1", JsonContent());
        var secondTenant1 = await client.PostAsync("/api/v1/events/tenant-1", JsonContent());
        var firstTenant2 = await client.PostAsync("/api/v1/events/tenant-2", JsonContent());

        Assert.Equal(HttpStatusCode.OK, firstTenant1.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondTenant1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, firstTenant2.StatusCode);
    }

    [Fact]
    public async Task AdminRateLimit_AppliesPerAuthenticatedUser()
    {
        using var server = BuildServer(new Dictionary<string, string?>
        {
            ["RateLimit:Enabled"] = "true",
            ["RateLimit:EventIngestionPermitLimit"] = "5",
            ["RateLimit:EventIngestionWindowSeconds"] = "60",
            ["RateLimit:AdminApiPermitLimit"] = "1",
            ["RateLimit:AdminApiWindowSeconds"] = "60",
        });

        using var client = server.CreateClient();

        var firstUserA = await SendAdminRequestAsync(client, "user-a");
        var secondUserA = await SendAdminRequestAsync(client, "user-a");
        var firstUserB = await SendAdminRequestAsync(client, "user-b");

        Assert.Equal(HttpStatusCode.OK, firstUserA.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondUserA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, firstUserB.StatusCode);
    }

    [Fact]
    public async Task RateLimitExceeded_ReturnsExpected429JsonAndRetryAfterHeader()
    {
        using var server = BuildServer(new Dictionary<string, string?>
        {
            ["RateLimit:Enabled"] = "true",
            ["RateLimit:EventIngestionPermitLimit"] = "1",
            ["RateLimit:EventIngestionWindowSeconds"] = "60",
            ["RateLimit:AdminApiPermitLimit"] = "5",
            ["RateLimit:AdminApiWindowSeconds"] = "60",
        });

        using var client = server.CreateClient();

        _ = await client.PostAsync("/api/v1/events/tenant-1", JsonContent());
        var limited = await client.PostAsync("/api/v1/events/tenant-1", JsonContent());

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.TryGetValues("Retry-After", out var retryAfterValues));

        var body = await limited.Content.ReadAsStringAsync();
        Assert.Contains("Rate limit exceeded. Please try again later.", body);
        Assert.Contains("\"statusCode\":429", body);
        Assert.Contains("\"traceId\":", body);
        Assert.NotEmpty(retryAfterValues!);
    }

    [Fact]
    public async Task RateLimitingCanBeDisabledByConfig()
    {
        using var server = BuildServer(new Dictionary<string, string?>
        {
            ["RateLimit:Enabled"] = "false",
            ["RateLimit:EventIngestionPermitLimit"] = "1",
            ["RateLimit:EventIngestionWindowSeconds"] = "60",
            ["RateLimit:AdminApiPermitLimit"] = "1",
            ["RateLimit:AdminApiWindowSeconds"] = "60",
        });

        using var client = server.CreateClient();

        var first = await client.PostAsync("/api/v1/events/tenant-1", JsonContent());
        var second = await client.PostAsync("/api/v1/events/tenant-1", JsonContent());
        var third = await client.PostAsync("/api/v1/events/tenant-1", JsonContent());

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendAdminRequestAsync(HttpClient client, string userId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/test");
        request.Headers.Add("x-sub", userId);
        return await client.SendAsync(request);
    }

    private static TestServer BuildServer(Dictionary<string, string?> configurationValues)
    {
        var builder = new WebHostBuilder()
            .ConfigureAppConfiguration(config => config.AddInMemoryCollection(configurationValues))
            .ConfigureServices((context, services) =>
            {
                services.AddRouting();
                services.AddLogging();
                services.AddHookBridgeRateLimiting(context.Configuration);
            })
            .Configure(app =>
            {
                app.Use(async (context, next) =>
                {
                    if (context.Request.Headers.TryGetValue("x-sub", out var sub) && !string.IsNullOrWhiteSpace(sub))
                    {
                        var identity = new ClaimsIdentity(new[] { new Claim("sub", sub.ToString()) }, "Test");
                        context.User = new ClaimsPrincipal(identity);
                    }

                    await next();
                });

                app.UseRouting();
                app.UseRateLimiter();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapPost("/api/v1/events/{tenantId}", async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status200OK;
                        await context.Response.WriteAsync("ok");
                    }).RequireRateLimiting(RateLimitingPolicyNames.EventIngestionPolicy);

                    endpoints.MapGet("/api/v1/admin/test", async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status200OK;
                        await context.Response.WriteAsync("ok");
                    }).RequireRateLimiting(RateLimitingPolicyNames.AdminApiPolicy);
                });
            });

        return new TestServer(builder);
    }

    private static StringContent JsonContent() => new("{}", Encoding.UTF8, "application/json");
}
