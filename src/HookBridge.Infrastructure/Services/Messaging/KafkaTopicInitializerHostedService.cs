using HookBridge.Application.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HookBridge.Infrastructure.Services.Messaging;

public sealed class KafkaTopicInitializerHostedService(
    IKafkaAdminService kafkaAdminService,
    ILogger<KafkaTopicInitializerHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Ensuring required Kafka topics exist before Kafka consumers and producers start.");
        await kafkaAdminService.EnsureTopicsAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
