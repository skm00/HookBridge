using HookBridge.Api.Middleware;
using HookBridge.Infrastructure.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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
