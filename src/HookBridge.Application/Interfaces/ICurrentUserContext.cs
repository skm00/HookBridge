namespace HookBridge.Application.Interfaces;

public interface ICurrentUserContext
{
    string? UserId { get; }

    string? TenantId { get; }

    string? Email { get; }

    string? Role { get; }

    bool IsAuthenticated { get; }
}
