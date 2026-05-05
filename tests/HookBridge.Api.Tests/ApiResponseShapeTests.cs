using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using FluentValidation;
using HookBridge.Api.Extensions;
using HookBridge.Api.Middleware;
using HookBridge.Api.RateLimiting;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace HookBridge.Api.Tests;

public sealed class ApiResponseShapeTests
{
    [Fact]
    public async Task UnhandledException_ReturnsStandardErrorShape()
    {
        using var server = BuildExceptionServer(app => app.MapGet("/boom", (HttpContext _) => throw new InvalidOperationException("boom")));
        using var client = server.CreateClient();

        var response = await client.GetFromJsonAsync<ApiErrorResponse>("/boom");

        Assert.NotNull(response);
        Assert.False(response!.Success);
        Assert.Equal("An unexpected error occurred.", response.Message);
        Assert.Equal(500, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(response.TraceId));
    }

    [Fact]
    public async Task ValidationException_ReturnsStandardValidationShape()
    {
        using var server = BuildExceptionServer(app => app.MapGet("/validation", (HttpContext _) => throw new FluentValidation.ValidationException("validation", new[] { new FluentValidation.Results.ValidationFailure("field", "is required") })));
        using var client = server.CreateClient();

        var response = await client.GetFromJsonAsync<ApiErrorResponse>("/validation");

        Assert.NotNull(response);
        Assert.False(response!.Success);
        Assert.Equal("Validation failed.", response.Message);
        Assert.Equal(400, response.StatusCode);
        Assert.NotNull(response.Errors);
        Assert.Contains("field", response.Errors!.Keys);
    }

    [Fact]
    public async Task ModelValidation_ReturnsStandardValidationShape()
    {
        using var server = BuildValidationServer();
        using var client = server.CreateClient();

        var result = await client.PostAsJsonAsync("/api/v1/test/validate", new { });
        var response = await result.Content.ReadFromJsonAsync<ApiErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.NotNull(response);
        Assert.False(response!.Success);
        Assert.Equal("Validation failed.", response.Message);
        Assert.Equal(400, response.StatusCode);
        Assert.NotNull(response.Errors);
    }

    [Fact]
    public async Task Unauthorized_ReturnsStandardShape()
    {
        using var server = BuildAuthServer();
        using var client = server.CreateClient();

        var result = await client.GetAsync("/secure/authorized");
        var response = await result.Content.ReadFromJsonAsync<ApiErrorResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        Assert.Equal("Unauthorized.", response!.Message);
    }

    [Fact]
    public async Task Forbidden_ReturnsStandardShape()
    {
        using var server = BuildAuthServer();
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("x-user", "viewer");

        var result = await client.GetAsync("/secure/forbidden");
        var response = await result.Content.ReadFromJsonAsync<ApiErrorResponse>();

        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        Assert.Equal("Forbidden.", response!.Message);
    }

    [Fact]
    public async Task RateLimit_ReturnsStandardShape()
    {
        using var server = BuildRateLimitServer();
        using var client = server.CreateClient();

        _ = await client.PostAsync("/api/v1/events/t1", JsonContent.Create(new { }));
        var limited = await client.PostAsync("/api/v1/events/t1", JsonContent.Create(new { }));
        var response = await limited.Content.ReadFromJsonAsync<ApiErrorResponse>();

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("Rate limit exceeded. Please try again later.", response!.Message);
        Assert.Equal(429, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(response.TraceId));
    }

    private static TestServer BuildExceptionServer(Action<WebApplication> mapEndpoints)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.UseMiddleware<GlobalExceptionMiddleware>();
        mapEndpoints(app);
        app.StartAsync().GetAwaiter().GetResult();
        return app.GetTestServer();
    }

    private static TestServer BuildRateLimitServer()
    {
        var builder = new WebHostBuilder()
            .ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:Enabled"] = "true",
                ["RateLimit:EventIngestionPermitLimit"] = "1",
                ["RateLimit:EventIngestionWindowSeconds"] = "60",
                ["RateLimit:AdminApiPermitLimit"] = "1",
                ["RateLimit:AdminApiWindowSeconds"] = "60",
            }))
            .ConfigureServices((context, services) =>
            {
                services.AddRouting();
                services.AddLogging();
                services.AddHookBridgeRateLimiting(context.Configuration);
            })
            .Configure(app =>
            {
                app.UseMiddleware<CorrelationIdMiddleware>();
                app.UseRouting();
                app.UseRateLimiter();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapPost("/api/v1/events/{tenantId}", () => Results.Ok())
                        .RequireRateLimiting(RateLimitingPolicyNames.EventIngestionPolicy);
                });
            });

        return new TestServer(builder);
    }

    private static TestServer BuildValidationServer()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddControllers();
                services.Configure<ApiBehaviorOptions>(options =>
                {
                    options.InvalidModelStateResponseFactory = context =>
                    {
                        var errors = context.ModelState
                            .Where(x => x.Value?.Errors.Count > 0)
                            .ToDictionary(x => x.Key, x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

                        return new BadRequestObjectResult(ApiResponseFactory.ValidationError(errors, context.HttpContext.TraceIdentifier));
                    };
                });
                services.AddApiVersioning(options => options.DefaultApiVersion = new ApiVersion(1, 0));
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });

        return new TestServer(builder);
    }

    private static TestServer BuildAuthServer()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                services.AddAuthorization(options => options.AddPolicy("Admin", p => p.RequireClaim(ClaimTypes.Role, "admin")));
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.Use(async (context, next) =>
                {
                    await next();
                    if (context.Response.StatusCode == 401)
                    {
                        await context.Response.WriteAsJsonAsync(ApiResponseFactory.Error("Unauthorized.", 401, context.TraceIdentifier));
                    }
                    else if (context.Response.StatusCode == 403)
                    {
                        await context.Response.WriteAsJsonAsync(ApiResponseFactory.Error("Forbidden.", 403, context.TraceIdentifier));
                    }
                });
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/secure/authorized", [Authorize] () => Results.Ok());
                    endpoints.MapGet("/secure/forbidden", [Authorize(Policy = "Admin")] () => Results.Ok());
                });
            });

        return new TestServer(builder);
    }

    [ApiController]
    [Route("api/v1/test")]
    private sealed class ValidationController : ControllerBase
    {
        [HttpPost("validate")]
        public IActionResult Validate([FromBody] ValidationRequest request) => Ok(request);
    }

    private sealed class ValidationRequest
    {
        [Required]
        public string Name { get; init; } = string.Empty;
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("x-user", out var user))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim> { new(ClaimTypes.Name, user.ToString()) };
            if (string.Equals(user.ToString(), "admin", StringComparison.OrdinalIgnoreCase))
            {
                claims.Add(new Claim(ClaimTypes.Role, "admin"));
            }

            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
