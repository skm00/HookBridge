namespace HookBridge.Application.Messaging;

public interface IKafkaAdminService
{
    Task EnsureTopicsAsync(CancellationToken cancellationToken = default);

    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
