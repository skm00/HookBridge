using System.Text;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using HookBridge.Api.Authorization;
using HookBridge.Api.Extensions;
using HookBridge.Api.Health;
using HookBridge.Api.Middleware;
using HookBridge.Api.Swagger;
using HookBridge.Api.Security;
using HookBridge.Application.DependencyInjection;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.DependencyInjection;
using Elastic.Apm.NetCoreAll;
using HookBridge.Infrastructure.Configuration;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var appEnvironment = builder.Environment;

var allowedCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? (appEnvironment.IsDevelopment()
        ? ["http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:3000", "http://127.0.0.1:3000"]
        : []);

builder.Services.AddCors(options =>
{
    options.AddPolicy("DashboardCors", policy =>
    {
        if (allowedCorsOrigins.Length > 0)
        {
            policy.WithOrigins(allowedCorsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        policy.SetIsOriginAllowed(_ => false)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Host.UseSerilog((context, _, loggerConfiguration) =>
    HookBridge.Infrastructure.Logging.SerilogConfigurationExtensions.ConfigureHookBridgeEcsLogging(
        loggerConfiguration,
        context.Configuration,
        "hookbridge-api"));

builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .ToDictionary(
                x => x.Key,
                x => x.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "The input was not valid." : e.ErrorMessage).ToArray());

        var response = ApiResponseFactory.ValidationError(errors, context.HttpContext.TraceIdentifier);
        return new BadRequestObjectResult(response);
    };
});

builder.Services.AddApiVersioning(options =>
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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddTransient<IConfigureOptions<Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions>, ConfigureSwaggerOptions>();
builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<SwaggerSecurityOperationFilter>();
    options.OperationFilter<SwaggerTagOperationFilter>();
    options.OperationFilter<SwaggerExamplesOperationFilter>();
    options.OperationFilter<SwaggerCommonResponsesOperationFilter>();
    options.SchemaFilter<SwaggerSensitiveSchemaFilter>();
    options.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT Bearer token.",
    });

    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "x-api-key",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "API key used for event ingestion.",
    });

    var xmlPaths = Directory.GetFiles(AppContext.BaseDirectory, "HookBridge.*.xml", SearchOption.TopDirectoryOnly);
    foreach (var xmlPath in xmlPaths)
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(
    builder.Configuration,
    builder.Environment,
    requireJwtSettings: true,
    requireStripeSettings: true);
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
builder.Services.AddScoped<TenantAccessValidator>();
builder.Services.AddScoped<IClientIpResolver, ClientIpResolver>();
builder.Services.AddScoped<IIpAllowlistService, IpAllowlistService>();
builder.Services.AddScoped<IElasticsearchHealthService, ElasticsearchHealthService>();
builder.Services.AddHookBridgeRateLimiting(builder.Configuration);

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
var jwtSecret = string.IsNullOrWhiteSpace(jwtSettings.Secret) ? new string('x', 32) : jwtSettings.Secret;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = "role",
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(
                    ApiResponseFactory.Error("Unauthorized.", StatusCodes.Status401Unauthorized, context.HttpContext.TraceIdentifier));
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(
                    ApiResponseFactory.Error("Forbidden.", StatusCodes.Status403Forbidden, context.HttpContext.TraceIdentifier));
            },
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.OwnerOnly, policy =>
        policy.RequireRole(AdminRole.Owner.ToString()));

    options.AddPolicy(AuthorizationPolicies.AdminOrOwner, policy =>
        policy.RequireRole(AdminRole.Owner.ToString(), AdminRole.Admin.ToString()));

    options.AddPolicy(AuthorizationPolicies.DeveloperOrAbove, policy =>
        policy.RequireRole(AdminRole.Owner.ToString(), AdminRole.Admin.ToString(), AdminRole.Developer.ToString()));

    options.AddPolicy(AuthorizationPolicies.ViewerOrAbove, policy =>
        policy.RequireRole(AdminRole.Owner.ToString(), AdminRole.Admin.ToString(), AdminRole.Developer.ToString(), AdminRole.Viewer.ToString()));
});

var elasticApmSettings = builder.Configuration.GetSection("ElasticApm").Get<ElasticApmSettings>() ?? new ElasticApmSettings();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

if (elasticApmSettings.Enabled)
{
    app.UseAllElasticApm(builder.Configuration);
}

if (app.Environment.IsDevelopment())
{
    var apiVersionDescriptionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
        {
            options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", $"HookBridge API {description.GroupName}");
        }
    });
}

app.UseCors("DashboardCors");

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHookBridgeHealthEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/v1/dev/demo/seed", async (
        IDemoDataSeeder seeder,
        IMongoRepository<Tenant> tenantRepository,
        IMongoRepository<AdminUser> adminUserRepository,
        IMongoRepository<ApiKey> apiKeyRepository,
        IMongoRepository<Subscription> subscriptionRepository,
        IMongoRepository<IncomingEvent> incomingEventRepository,
        IMongoRepository<DeliveryAttempt> deliveryAttemptRepository,
        IMongoRepository<FailedEvent> failedEventRepository,
        IMongoRepository<Notification> notificationRepository,
        IMongoRepository<AuditLog> auditLogRepository,
        CancellationToken cancellationToken) =>
    {
        await seeder.SeedAsync(cancellationToken);

        var tenant = await tenantRepository.FirstOrDefaultAsync(x => x.Slug == "demo-company", cancellationToken);
        var tenantId = tenant?.Id;

        var adminCount = tenantId is null
            ? 0
            : (await adminUserRepository.FindAsync(x => x.TenantId == tenantId, cancellationToken)).Count;
        var apiKeyCount = tenantId is null
            ? 0
            : (await apiKeyRepository.FindAsync(x => x.TenantId == tenantId, cancellationToken)).Count;
        var subscriptionCount = tenantId is null
            ? 0
            : (await subscriptionRepository.FindAsync(x => x.TenantId == tenantId, cancellationToken)).Count;
        var incomingEventCount = tenantId is null
            ? 0
            : (await incomingEventRepository.FindAsync(x => x.TenantId == tenantId, cancellationToken)).Count;
        var deliveryAttemptCount = tenantId is null
            ? 0
            : (await deliveryAttemptRepository.FindAsync(x => x.TenantId == tenantId, cancellationToken)).Count;
        var failedEventCount = tenantId is null
            ? 0
            : (await failedEventRepository.FindAsync(x => x.TenantId == tenantId, cancellationToken)).Count;
        var notificationCount = tenantId is null
            ? 0
            : (await notificationRepository.FindAsync(x => x.TenantId == tenantId, cancellationToken)).Count;
        var auditLogCount = tenantId is null
            ? 0
            : (await auditLogRepository.FindAsync(x => x.TenantId == tenantId, cancellationToken)).Count;

        return Results.Ok(new
        {
            TenantCount = tenant is null ? 0 : 1,
            AdminUserCount = adminCount,
            ApiKeyCount = apiKeyCount,
            SubscriptionCount = subscriptionCount,
            IncomingEventCount = incomingEventCount,
            DeliveryAttemptCount = deliveryAttemptCount,
            FailedEventCount = failedEventCount,
            NotificationCount = notificationCount,
            AuditLogCount = auditLogCount,
        });
    });
}

if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("DemoData:Enabled"))
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<IDemoDataSeeder>();
    await seeder.SeedAsync();
    Log.Information("Demo seed completed on startup because DemoData:Enabled is true.");
}

app.Run();

internal sealed class ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider) : IConfigureOptions<Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions>
{
    public void Configure(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options)
    {
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "HookBridge API",
                Description = "Multi-tenant webhook delivery platform with retry, DLQ, authentication, logs, and monitoring.",
                Version = description.GroupName,
            });
        }
    }
}
