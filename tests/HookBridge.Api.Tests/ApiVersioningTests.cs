using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Linq;
using System.Text.Encodings.Web;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using HookBridge.Api.Controllers;
using HookBridge.Api.Security;
using HookBridge.Api.Swagger;
using HookBridge.Application.DTOs.Events;
using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Security;
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
        var client = host.CreateClient();
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
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");

        var response = await client.PostAsJsonAsync("/api/v1/admin/subscriptions", new CreateSubscriptionRequestDto
        {
                        EventType = "order.created",
            TargetUrl = "https://example.com/webhooks",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerV1Document_IsAvailable()
    {
        using var host = await BuildVersionedHostAsync();
        var client = host.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerV1Document_ContainsBearerAndApiKeySecuritySchemes()
    {
        using var host = await BuildVersionedHostAsync();
        var client = host.CreateClient();
        using var swagger = await GetSwaggerDocumentAsync(client);

        var securitySchemes = swagger.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes");

        Assert.True(securitySchemes.TryGetProperty("Bearer", out _));
        Assert.True(securitySchemes.TryGetProperty("ApiKey", out _));
    }

    [Fact]
    public async Task AdminEndpoint_DocumentsBearerSecurity()
    {
        using var host = await BuildVersionedHostAsync();
        var client = host.CreateClient();
        using var swagger = await GetSwaggerDocumentAsync(client);

        var createSubscription = swagger.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v{version}/admin/subscriptions")
            .GetProperty("post");

        var securityItem = createSubscription.GetProperty("security")[0];
        Assert.True(securityItem.TryGetProperty("Bearer", out _));
    }

    [Fact]
    public async Task EventIngestionEndpoint_DocumentsApiKeySecurity()
    {
        using var host = await BuildVersionedHostAsync();
        var client = host.CreateClient();
        using var swagger = await GetSwaggerDocumentAsync(client);

        var ingestion = swagger.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v{version}/events/{tenantId}")
            .GetProperty("post");

        var securityItem = ingestion.GetProperty("security")[0];
        Assert.True(securityItem.TryGetProperty("ApiKey", out _));
    }

    [Fact]
    public async Task SensitiveFields_AreNotExposedInSwaggerSchemas()
    {
        using var host = await BuildVersionedHostAsync();
        var client = host.CreateClient();
        using var swagger = await GetSwaggerDocumentAsync(client);

        var schemas = swagger.RootElement.GetProperty("components").GetProperty("schemas").EnumerateObject();
        foreach (var schema in schemas)
        {
            if (!schema.Value.TryGetProperty("properties", out var properties))
            {
                continue;
            }

            var propertyNames = properties.EnumerateObject().Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("keyHash", propertyNames);
            Assert.DoesNotContain("passwordHash", propertyNames);
            Assert.DoesNotContain("clientSecret", propertyNames);
            Assert.DoesNotContain("hmacSecret", propertyNames);
            Assert.DoesNotContain("secretKey", propertyNames);
            Assert.DoesNotContain("webhookSecret", propertyNames);
            Assert.DoesNotContain("jwtSecret", propertyNames);
            Assert.DoesNotContain("masterKey", propertyNames);
            Assert.DoesNotContain("encryptionMasterKey", propertyNames);
        }
    }

    [Fact]
    public async Task ApiReportsSupportedVersions()
    {
        using var host = await BuildVersionedHostAsync();
        var client = host.CreateClient();
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
        var client = host.CreateClient();

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
                    options.OperationFilter<SwaggerSecurityOperationFilter>();
                    options.SchemaFilter<SwaggerSensitiveSchemaFilter>();
                    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer" });
                    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey, Name = "x-api-key", In = ParameterLocation.Header });
                    options.DocInclusionPredicate((_, apiDesc) =>
                        apiDesc.RelativePath is not null &&
                        (apiDesc.RelativePath.Contains("events") || apiDesc.RelativePath.Contains("admin/subscriptions")));
                });

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                services.AddAuthorization(options =>
                {
                    options.AddPolicy("AdminOrOwner", p => p.RequireAuthenticatedUser());
                    options.AddPolicy("DeveloperOrAbove", p => p.RequireAuthenticatedUser());
                });

                services.AddSingleton<IEventIngestionService, FakeEventIngestionService>();
                services.AddSingleton<IApiKeyService, FakeApiKeyService>();
                services.AddSingleton<IWebhookSignatureValidator, FakeWebhookSignatureValidator>();
                services.AddSingleton<IClientIpResolver, ClientIpResolver>();
                services.AddSingleton<IIpAllowlistService, IpAllowlistService>();
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

    private static async Task<JsonDocument> GetSwaggerDocumentAsync(HttpClient client)
    {
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json);
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

    private sealed class FakeApiKeyService : IApiKeyService
    {
        public Task<HookBridge.Application.DTOs.ApiKeys.CreateApiKeyResponseDto> CreateAsync(string tenantId, HookBridge.Application.DTOs.ApiKeys.CreateApiKeyRequestDto request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<HookBridge.Application.DTOs.ApiKeys.ApiKeyResponseDto>> GetByTenantAsync(string tenantId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<HookBridge.Application.DTOs.ApiKeys.ApiKeyResponseDto?> UpdateAsync(string tenantId, string keyId, HookBridge.Application.DTOs.ApiKeys.UpdateApiKeyRequestDto request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<bool> RevokeAsync(string tenantId, string keyId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<HookBridge.Application.DTOs.ApiKeys.ApiKeyValidationResult> ValidateAsync(string tenantId, string plainApiKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new HookBridge.Application.DTOs.ApiKeys.ApiKeyValidationResult { IsValid = true, TenantId = tenantId, ApiKeyId = "key-1" });
    }

    private sealed class FakeWebhookSignatureValidator : IWebhookSignatureValidator
    {
        public bool Validate(string payload, string signatureHeader, string secret) => true;
    }

    private sealed class FakeSubscriptionService : ISubscriptionService
    {
        public Task<SubscriptionResponseDto> CreateAsync(string tenantId, CreateSubscriptionRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new SubscriptionResponseDto
            {
                Id = "sub_1",
                                EventType = request.EventType,
                TargetUrl = request.TargetUrl,
                CreatedAt = DateTime.UtcNow,
            });

        public Task<bool> DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> DisableAsync(string tenantId, string id, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> EnableAsync(string tenantId, string id, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<SubscriptionResponseDto?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default) => Task.FromResult<SubscriptionResponseDto?>(null);
        public Task<HookBridge.Application.DTOs.Common.PagedResponseDto<SubscriptionResponseDto>> SearchAsync(SubscriptionSearchRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult(HookBridge.Application.DTOs.Common.PagedResponseDto<SubscriptionResponseDto>.Create([], 1, 50, 0));
        public Task<SubscriptionResponseDto?> UpdateAsync(string tenantId, string id, UpdateSubscriptionRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<SubscriptionResponseDto?>(null);
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
