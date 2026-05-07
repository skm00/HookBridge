using Confluent.Kafka;
using Confluent.Kafka.Admin;
using HookBridge.Application.Messaging;
using HookBridge.Shared.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HookBridge.Infrastructure.Services.Messaging;

public sealed class KafkaAdminService : IKafkaAdminService
{
    private readonly AdminClientConfig _adminConfig;
    private readonly ILogger<KafkaAdminService> _logger;

    public KafkaAdminService(IConfiguration configuration, ILogger<KafkaAdminService> logger)
    {
        _logger = logger;

        var bootstrapServers = configuration.GetValue<string>("Kafka:BootstrapServers")
            ?? throw new ArgumentNullException("Kafka:BootstrapServers is missing.");

        _adminConfig = new AdminClientConfig
        {
            BootstrapServers = bootstrapServers
        };
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var adminClient = new AdminClientBuilder(_adminConfig).Build();
            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(3));

            return Task.FromResult(metadata.Brokers.Count > 0);
        }
        catch (KafkaException ex)
        {
            _logger.LogWarning(ex, "Kafka health check failed. Broker is unreachable.");
            return Task.FromResult(false);
        }
    }

    public async Task EnsureTopicsAsync(CancellationToken cancellationToken = default)
    {
        using var adminClient = new AdminClientBuilder(_adminConfig).Build();

        var requiredTopics = new[]
        {
            KafkaTopics.WebhookEvents,
            KafkaTopics.WebhookRetry,
            KafkaTopics.WebhookDlq,
        };

        try
        {
            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));
            var existingTopics = metadata.Topics
                .Where(topicMetadata => topicMetadata.Error.Code == ErrorCode.NoError)
                .Select(topicMetadata => topicMetadata.Topic)
                .ToHashSet(StringComparer.Ordinal);

            var missingTopics = requiredTopics
                .Where(topic => !existingTopics.Contains(topic))
                .Select(topic => new TopicSpecification
                {
                    Name = topic,
                    ReplicationFactor = 1,
                    NumPartitions = 1,
                })
                .ToArray();

            if (missingTopics.Length == 0)
            {
                _logger.LogInformation("All required Kafka topics already exist. Topics: {Topics}", requiredTopics);
                return;
            }

            _logger.LogInformation("Creating required Kafka topics: {Topics}", missingTopics.Select(topic => topic.Name));

            await adminClient.CreateTopicsAsync(missingTopics);

            _logger.LogInformation("Required Kafka topics created successfully: {Topics}", missingTopics.Select(topic => topic.Name));
        }
        catch (CreateTopicsException ex)
        {
            var nonIgnorableResults = ex.Results
                .Where(result => result.Error.Code != ErrorCode.TopicAlreadyExists)
                .ToArray();

            if (nonIgnorableResults.Length == 0)
            {
                _logger.LogInformation("Required Kafka topics were created by another process before this instance completed initialization.");
                return;
            }

            _logger.LogError(ex, "Failed to create required Kafka topics.");
            throw;
        }
    }
}
