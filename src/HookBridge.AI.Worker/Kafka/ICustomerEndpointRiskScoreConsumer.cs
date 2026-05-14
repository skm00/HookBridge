namespace HookBridge.AI.Worker.Kafka;

public interface ICustomerEndpointRiskScoreConsumer
{
    IAsyncEnumerable<CustomerEndpointRiskScoreMessage> ConsumeAsync(CancellationToken cancellationToken = default);
}
