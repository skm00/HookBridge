namespace HookBridge.Application.DTOs.Tenants;

/// <summary>
/// Request payload to create a tenant.
/// </summary>
public sealed class CreateTenantRequestDto
{
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? ContactEmail { get; set; }

    public List<string> NotificationEmails { get; set; } = [];
}
