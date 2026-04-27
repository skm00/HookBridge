namespace HookBridge.Application.Messaging;

public interface IKafkaConsumer
{
    IAsyncEnumerable<T> ConsumeAsync<T>(
        string topic,
        string groupId,
        CancellationToken cancellationToken = default);
}
