using HookBridge.Application.Interfaces;

namespace HookBridge.Worker;

public sealed class WorkerCurrentUserContext : ICurrentUserContext
{
    public string? UserId => null;

    public string? TenantId => null;

    public string? Email => null;

    public string? Role => null;

    public bool IsAuthenticated => false;
}
