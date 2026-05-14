using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public sealed class WebhookFailureAnomalyDetectionMessage
{
    private readonly Func<CancellationToken, Task> _acknowledgeAsync;

    public WebhookFailureAnomalyDetectionMessage(WebhookFailureAnomalyDetectionRequestDto request, Func<CancellationToken, Task> acknowledgeAsync)
    {
        Request = request;
        _acknowledgeAsync = acknowledgeAsync;
    }

    public WebhookFailureAnomalyDetectionRequestDto Request { get; }

    public Task AcknowledgeAsync(CancellationToken cancellationToken = default) => _acknowledgeAsync(cancellationToken);
}
