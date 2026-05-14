using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public sealed class FluentValidationRuleGenerationMessage
{
    private readonly Func<CancellationToken, Task> _acknowledgeAsync;

    public FluentValidationRuleGenerationMessage(
        FluentValidationRuleGenerationRequestDto request,
        Func<CancellationToken, Task> acknowledgeAsync)
    {
        Request = request;
        _acknowledgeAsync = acknowledgeAsync;
    }

    public FluentValidationRuleGenerationRequestDto Request { get; }

    public Task AcknowledgeAsync(CancellationToken cancellationToken = default) => _acknowledgeAsync(cancellationToken);
}
