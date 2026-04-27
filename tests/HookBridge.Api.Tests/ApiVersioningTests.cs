using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using HookBridge.Api.Controllers;
using HookBridge.Api.Security;
using HookBridge.Application.DTOs.Events;
using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Xunit;

namespace HookBridge.Api.Tests;

public sealed class ApiVersioningTests
{
    [Fact]
    public async Task EventsRoute_V1_Works()
    {
        using var host = await BuildVersionedHostAsync();
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("x-api-key", "hb_test_key");

        var response = await client.PostAsJsonAsync("/api/v1/events/tenant-1", new EventIngestionRequestDto
        {
            EventType = "order.created",
            EventId = "evt_1",
            Data = new { orderId = "123" },
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task AdminSubscriptionsRoute_V1_Works()
    {
        using var host = await BuildVersionedHostAsync();
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");

        var response = await client.PostAsJsonAsync("/api/v1/admin/subscriptions", new CreateSubscriptionRequestDto
        {
            TenantId = "tenant-1",
            EventType = "order.created",
            TargetUrl = "https://example.com/webhooks",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerV1Document_IsAvailable()
    {
        using var host = await BuildVersionedHostAsync();
        var client = host.GetTestClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiReportsSupportedVersions()
    {
        using var host = await BuildVersionedHostAsync();
        var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("x-api-key", "hb_test_key");

        var response = await client.PostAsJsonAsync("/api/v1/events/tenant-1", new EventIngestionRequestDto
        {
            EventType = "order.created",
            EventId = "evt_1",
            Data = new { orderId = "123" },
        });

        Assert.True(response.Headers.TryGetValues("api-supported-versions", out var versions));
        Assert.Contains("1.0", Assert.Single(versions));
    }

    [Fact]
    public async Task UnversionedRoute_Returns404()
    {
        using var host = await BuildVersionedHostAsync();
        var client = host.GetTestClient();

        var response = await client.PostAsJsonAsync("/api/events/tenant-1", new EventIngestionRequestDto
        {
            EventType = "order.created",
            EventId = "evt_1",
            Data = new { orderId = "123" },
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<TestServer> BuildVersionedHostAsync()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddHttpContextAccessor();
                services.AddControllers().AddApplicationPart(typeof(EventsController).Assembly);
                services.AddApiVersioning(options =>
                {
                    options.DefaultApiVersion = new ApiVersion(1, 0);
                    options.AssumeDefaultVersionWhenUnspecified = true;
                    options.ReportApiVersions = true;
                    options.ApiVersionReader = new UrlSegmentApiVersionReader();
                }).AddApiExplorer(options =>
                {
                    options.GroupNameFormat = "'v'V";
                    options.SubstituteApiVersionInUrl = true;
                });

                services.AddTransient<IConfigureOptions<Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions>, TestConfigureSwaggerOptions>();
                services.AddSwaggerGen(options =>
                {
                    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer" });
                });

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                services.AddAuthorization();

                services.AddSingleton<IEventIngestionService, FakeEventIngestionService>();
                services.AddSingleton<ISubscriptionService, FakeSubscriptionService>();
                services.AddSingleton<ICurrentUserContext>(new FakeCurrentUserContext());
                services.AddScoped<TenantAccessValidator>();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseSwagger();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });

        var server = new TestServer(builder);
        await Task.CompletedTask;
        return server;
    }

    private sealed class TestConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
        : IConfigureOptions<Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions>
    {
        public void Configure(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options)
        {
            foreach (var description in provider.ApiVersionDescriptions)
            {
                options.SwaggerDoc(description.GroupName, new OpenApiInfo
                {
                    Title = $"HookBridge API {description.GroupName}",
                    Version = description.GroupName,
                });
            }
        }
    }

    private sealed class FakeEventIngestionService : IEventIngestionService
    {
        public Task<EventIngestionResponseDto> IngestAsync(string tenantId, string apiKey, EventIngestionRequestDto request, string? correlationId, CancellationToken cancellationToken = default)
            => Task.FromResult(new EventIngestionResponseDto
            {
                Status = "accepted",
                EventId = request.EventId,
                Message = "Event accepted for delivery.",
            });
    }

    private sealed class FakeSubscriptionService : ISubscriptionService
    {
        public Task<SubscriptionResponseDto> CreateAsync(CreateSubscriptionRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new SubscriptionResponseDto
            {
                Id = "sub_1",
                TenantId = request.TenantId,
                EventType = request.EventType,
                TargetUrl = request.TargetUrl,
                CreatedAt = DateTime.UtcNow,
            });

        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> DisableAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> EnableAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<SubscriptionResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<SubscriptionResponseDto?>(null);
        public Task<IReadOnlyList<SubscriptionResponseDto>> SearchAsync(SubscriptionSearchRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SubscriptionResponseDto>>([]);
        public Task<SubscriptionResponseDto?> UpdateAsync(string id, UpdateSubscriptionRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<SubscriptionResponseDto?>(null);
    }

    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public string? UserId => "user-1";
        public string? TenantId => "tenant-1";
        public string? Email => "owner@hookbridge.dev";
        public string? Role => "Owner";
        public bool IsAuthenticated => true;
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim("role", "Owner"),
            ],
            Scheme.Name);

            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
