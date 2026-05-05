using HookBridge.Api.Health;
using HookBridge.Application.Messaging;
using HookBridge.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace HookBridge.Api.Extensions;

public static class HealthEndpointRouteBuilderExtensions
{
    public static void MapHookBridgeHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
            .AllowAnonymous()
            .WithTags("Health")
            .WithSummary("Returns a lightweight liveness status.");

        app.MapGet("/api/v{version:apiVersion}/health/mongodb", async ([FromServices] IMongoDatabase database, CancellationToken cancellationToken) =>
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
        })
            .AllowAnonymous()
            .WithTags("Health")
            .WithSummary("Checks MongoDB connectivity.");

        app.MapGet("/api/v{version:apiVersion}/health/kafka", async (IKafkaAdminService kafkaAdminService, CancellationToken cancellationToken) =>
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
        })
            .AllowAnonymous()
            .WithTags("Health")
            .WithSummary("Checks Kafka connectivity.");

        app.MapGet("/api/v{version:apiVersion}/health/apm", (IOptions<ElasticApmSettings> apmOptions) =>
        {
            var enabled = apmOptions.Value.Enabled;

            return Results.Ok(new
            {
                service = "ElasticAPM",
                isHealthy = enabled,
                message = enabled ? "Elastic APM is enabled." : "Elastic APM is disabled.",
            });
        })
            .AllowAnonymous()
            .WithTags("Health")
            .WithSummary("Reports Elastic APM feature state.");

        app.MapGet("/api/v{version:apiVersion}/health/worker", () => Results.Ok(new
        {
            service = "Worker",
            isHealthy = true,
            message = "Worker health endpoint is reachable.",
        }))
            .AllowAnonymous()
            .WithTags("Health")
            .WithSummary("Reports worker health endpoint availability.");

        app.MapGet("/api/v{version:apiVersion}/health/elasticsearch", async (IElasticsearchHealthService elasticsearchHealthService, CancellationToken cancellationToken) =>
        {
            var response = await elasticsearchHealthService.CheckHealthAsync(cancellationToken);

            return Results.Ok(new
            {
                service = response.Service,
                isHealthy = response.IsHealthy,
                message = response.Message,
            });
        })
            .AllowAnonymous()
            .WithTags("Health")
            .WithSummary("Checks Elasticsearch connectivity.");
    }
}
