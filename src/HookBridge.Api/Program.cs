using System.Reflection;
using System.Text;
using HookBridge.Api.Authorization;
using HookBridge.Api.Extensions;
using HookBridge.Api.Health;
using HookBridge.Api.Middleware;
using HookBridge.Api.Security;
using HookBridge.Application.DependencyInjection;
using HookBridge.Application.Interfaces;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.Configuration;
using HookBridge.Infrastructure.DependencyInjection;
using HookBridge.Infrastructure.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, loggerConfiguration) =>
    loggerConfiguration.ConfigureHookBridgeEcsLogging(context.Configuration, "hookbridge-api"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "HookBridge API",
        Version = "v1",
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT Bearer token.",
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            },
            Array.Empty<string>()
        },
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
builder.Services.AddScoped<TenantAccessValidator>();
builder.Services.AddScoped<IElasticsearchHealthService, ElasticsearchHealthService>();

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings are missing.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = "role",
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

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHookBridgeHealthEndpoints();

app.Run();
