using HookBridge.Application.DTOs.Tenants;

namespace HookBridge.Application.Interfaces.Services;

/// <summary>
/// Tenant business operations.
/// </summary>
public interface ITenantService
{
    Task<TenantResponseDto> CreateAsync(CreateTenantRequestDto request, CancellationToken cancellationToken = default);

    Task<TenantResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<TenantResponseDto?> UpdateAsync(string id, UpdateTenantRequestDto request, CancellationToken cancellationToken = default);

    Task<bool> DisableAsync(string id, CancellationToken cancellationToken = default);
}
