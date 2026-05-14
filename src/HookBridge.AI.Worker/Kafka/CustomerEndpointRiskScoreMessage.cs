using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public sealed class CustomerEndpointRiskScoreMessage
{
    private readonly Func<CancellationToken, Task> _acknowledgeAsync;

    public CustomerEndpointRiskScoreMessage(CustomerEndpointRiskScoreRequestDto request, Func<CancellationToken, Task> acknowledgeAsync)
    {
        Request = request;
        _acknowledgeAsync = acknowledgeAsync;
    }

    public CustomerEndpointRiskScoreRequestDto Request { get; }

    public Task AcknowledgeAsync(CancellationToken cancellationToken = default) => _acknowledgeAsync(cancellationToken);
}
