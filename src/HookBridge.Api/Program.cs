using System.Reflection;
using System.Text;
using HookBridge.Api.Middleware;
using HookBridge.Application.DependencyInjection;
using HookBridge.Application.Messaging;
using HookBridge.Infrastructure.Configuration;
using HookBridge.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

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
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/v1/health/mongodb", async (IMongoDatabase database, CancellationToken cancellationToken) =>
{
    try
    {
        await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);

        return Results.Ok(new
        {
            service = "MongoDB",
            isHealthy = true,
            message = "MongoDB connection is healthy.",
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            service = "MongoDB",
            isHealthy = false,
            message = $"MongoDB connection failed. Reason: {ex.Message}",
        });
    }
});

app.MapGet("/api/v1/health/kafka", async (IKafkaAdminService kafkaAdminService, CancellationToken cancellationToken) =>
{
    try
    {
        var isHealthy = await kafkaAdminService.IsHealthyAsync(cancellationToken);

        if (isHealthy)
        {
            return Results.Ok(new
            {
                service = "Kafka",
                isHealthy = true,
                message = "Kafka connection is healthy.",
            });
        }

        return Results.Ok(new
        {
            service = "Kafka",
            isHealthy = false,
            message = "Kafka connection failed. Reason: Health check returned unhealthy status.",
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            service = "Kafka",
            isHealthy = false,
            message = $"Kafka connection failed. Reason: {ex.Message}",
        });
    }
});

app.Run();
