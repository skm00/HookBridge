namespace HookBridge.AI.Worker.Kafka;

public interface IFluentValidationRuleGenerationConsumer
{
    IAsyncEnumerable<FluentValidationRuleGenerationMessage> ConsumeAsync(CancellationToken cancellationToken = default);
}
