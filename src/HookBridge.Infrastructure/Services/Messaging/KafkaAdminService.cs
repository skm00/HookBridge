using Confluent.Kafka;
using Confluent.Kafka.Admin;
using HookBridge.Application.Messaging;
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
        const string requiredTopic = "webhook-events";

        try
        {
            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));
            var topicExists = metadata.Topics.Any(t => t.Topic == requiredTopic);

            if (!topicExists)
            {
                _logger.LogInformation("Topic '{Topic}' not found. Creating it now...", requiredTopic);

                await adminClient.CreateTopicsAsync(new[]
                {
                    new TopicSpecification
                    {
                        Name = requiredTopic,
                        ReplicationFactor = 1,
                        NumPartitions = 1
                    }
                });

                _logger.LogInformation("Topic '{Topic}' created successfully.", requiredTopic);
            }
        }
        catch (CreateTopicsException ex)
        {
            _logger.LogError(ex, "Failed to create required Kafka topics.");
            throw;
        }
    }
}
