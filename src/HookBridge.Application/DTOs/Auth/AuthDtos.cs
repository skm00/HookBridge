namespace HookBridge.Application.DTOs.Auth;

public sealed class RegisterAdminRequestDto
{
    public string TenantId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
}

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
}

public sealed class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public AdminUserResponseDto User { get; set; } = new();
}
