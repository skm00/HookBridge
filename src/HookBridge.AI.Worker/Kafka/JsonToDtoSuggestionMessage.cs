using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public sealed class JsonToDtoSuggestionMessage
{
    private readonly Func<CancellationToken, Task> _acknowledgeAsync;

    public JsonToDtoSuggestionMessage(
        JsonToDtoSuggestionRequestDto request,
        Func<CancellationToken, Task> acknowledgeAsync)
    {
        Request = request;
        _acknowledgeAsync = acknowledgeAsync;
    }

    public JsonToDtoSuggestionRequestDto Request { get; }

    public Task AcknowledgeAsync(CancellationToken cancellationToken = default)
        => _acknowledgeAsync(cancellationToken);
}
