using HookBridge.Application.Messaging;

namespace HookBridge.Infrastructure.Services.Messaging;

public sealed class KafkaAdminServicePlaceholder : IKafkaAdminService
{
    public Task EnsureTopicsAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}
