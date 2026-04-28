using HookBridge.Application.DTOs.ApiKeys;

namespace HookBridge.Application.Interfaces.Services;

public interface IApiKeyService
{
    Task<CreateApiKeyResponseDto> CreateAsync(string tenantId, CreateApiKeyRequestDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApiKeyResponseDto>> GetByTenantAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<ApiKeyResponseDto?> UpdateAsync(string tenantId, string keyId, UpdateApiKeyRequestDto request, CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(string tenantId, string keyId, CancellationToken cancellationToken = default);

    Task<ApiKeyValidationResult> ValidateAsync(string tenantId, string plainApiKey, CancellationToken cancellationToken = default);
}
