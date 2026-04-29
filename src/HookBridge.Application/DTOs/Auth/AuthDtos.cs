using HookBridge.Domain.Enums;
namespace HookBridge.Application.DTOs.Auth;

/// <summary>
/// Request payload used to register a new tenant workspace owner/admin.
/// </summary>
public sealed class RegisterAdminRequestDto
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string? OrganizationName { get; set; }
}

/// <summary>
/// Request payload used to exchange credentials for an access token.
/// </summary>
public sealed class LoginRequestDto
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public sealed class AdminUserResponseDto
{
    public string Id { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public AdminRole Role { get; set; }

    public string OrganizationName { get; set; } = string.Empty;
}

/// <summary>
/// Authentication response that includes a JWT and current user details.
/// </summary>
public sealed class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public AdminUserResponseDto User { get; set; } = new();
}
