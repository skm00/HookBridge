using System.Reflection;
using HookBridge.Api.Middleware;
using HookBridge.Application.DependencyInjection;
using HookBridge.Infrastructure.DependencyInjection;
using Microsoft.OpenApi.Models;
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

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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

app.Run();
