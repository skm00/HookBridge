namespace HookBridge.Application.DTOs.Tenants;

/// <summary>
/// Request payload to update mutable tenant fields.
/// </summary>
public sealed class UpdateTenantRequestDto
{
    public string? Name { get; set; }

    public string? ContactEmail { get; set; }

    public List<string> NotificationEmails { get; set; } = [];
}
