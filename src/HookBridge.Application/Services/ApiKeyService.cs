using FluentValidation;
using HookBridge.Application.DTOs.ApiKeys;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Security;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace HookBridge.Application.Services;

public sealed class ApiKeyService(
    IMongoRepository<ApiKey> apiKeyRepository,
    IMongoRepository<Tenant> tenantRepository,
    IAuditLogService auditLogService,
    IGuidGenerator guidGenerator,
    IDateTimeProvider dateTimeProvider,
    IApiKeyGenerator apiKeyGenerator,
    IApiKeyHasher apiKeyHasher,
    IValidator<CreateApiKeyRequestDto> createValidator,
    ILogger<ApiKeyService> logger) : IApiKeyService
{
    public async Task<CreateApiKeyResponseDto> CreateAsync(string tenantId, CreateApiKeyRequestDto request, CancellationToken cancellationToken = default)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            throw new KeyNotFoundException($"Tenant '{tenantId}' not found.");
        }

        if (tenant.Status != TenantStatus.Active)
        {
            throw new ConflictException($"Tenant '{tenantId}' is not active.");
        }

        var plainApiKey = apiKeyGenerator.Generate();
        var now = dateTimeProvider.UtcNow;

        var apiKey = new ApiKey
        {
            Id = guidGenerator.NewGuid(),
            TenantId = tenantId,
            Name = request.Name,
            KeyHash = apiKeyHasher.Hash(plainApiKey),
            KeyPrefix = apiKeyGenerator.GetKeyPrefix(plainApiKey),
            IsActive = true,
            LastUsedAt = null,
            RevokedAt = null,
            CreatedAt = now,
            UpdatedAt = null,
        };

        await apiKeyRepository.AddAsync(apiKey, cancellationToken);
        await TryAuditAsync(
            new AuditLog
            {
                TenantId = tenantId,
                Action = "ApiKeyCreated",
                ResourceType = nameof(ApiKey),
                ResourceId = apiKey.Id,
                Description = $"API key '{apiKey.Name}' created.",
                Metadata = new Dictionary<string, object?>
                {
                    ["name"] = apiKey.Name,
                    ["keyPrefix"] = apiKey.KeyPrefix,
                },
            },
            cancellationToken);

        return new CreateApiKeyResponseDto
        {
            PlainApiKey = plainApiKey,
            ApiKey = Map(apiKey),
        };
    }

    public async Task<IReadOnlyList<ApiKeyResponseDto>> GetByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            throw new KeyNotFoundException($"Tenant '{tenantId}' not found.");
        }

        var keys = await apiKeyRepository.FindAsync(x => x.TenantId == tenantId, cancellationToken);
        return keys.Select(Map).ToList();
    }

    public async Task<bool> RevokeAsync(string tenantId, string keyId, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return false;
        }

        var apiKey = await apiKeyRepository.GetByIdAsync(keyId, cancellationToken);
        if (apiKey is null || apiKey.TenantId != tenantId)
        {
            return false;
        }

        apiKey.IsActive = false;
        apiKey.RevokedAt = dateTimeProvider.UtcNow;
        apiKey.UpdatedAt = apiKey.RevokedAt;

        await apiKeyRepository.UpdateAsync(apiKey, cancellationToken);
        await TryAuditAsync(
            new AuditLog
            {
                TenantId = tenantId,
                Action = "ApiKeyRevoked",
                ResourceType = nameof(ApiKey),
                ResourceId = apiKey.Id,
                Description = $"API key '{apiKey.Name}' revoked.",
                Metadata = new Dictionary<string, object?>
                {
                    ["name"] = apiKey.Name,
                    ["keyPrefix"] = apiKey.KeyPrefix,
                },
            },
            cancellationToken);
        return true;
    }

    public async Task<ApiKeyValidationResult> ValidateAsync(string tenantId, string plainApiKey, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return Invalid("tenant_not_found");
        }

        if (tenant.Status != TenantStatus.Active)
        {
            return Invalid("tenant_inactive");
        }

        var keyHash = apiKeyHasher.Hash(plainApiKey);
        var apiKey = await apiKeyRepository.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.KeyHash == keyHash, cancellationToken);

        if (apiKey is null)
        {
            return Invalid("api_key_not_found");
        }

        if (!apiKey.IsActive)
        {
            return Invalid("api_key_revoked");
        }

        if (!apiKeyHasher.Verify(plainApiKey, apiKey.KeyHash))
        {
            return Invalid("api_key_invalid");
        }

        apiKey.LastUsedAt = dateTimeProvider.UtcNow;
        apiKey.UpdatedAt = apiKey.LastUsedAt;
        await apiKeyRepository.UpdateAsync(apiKey, cancellationToken);

        return new ApiKeyValidationResult
        {
            IsValid = true,
            TenantId = tenantId,
            ApiKeyId = apiKey.Id,
            FailureReason = null,
        };
    }

    private static ApiKeyResponseDto Map(ApiKey apiKey) => new()
    {
        Id = apiKey.Id,
        TenantId = apiKey.TenantId,
        Name = apiKey.Name,
        KeyPrefix = apiKey.KeyPrefix,
        IsActive = apiKey.IsActive,
        LastUsedAt = apiKey.LastUsedAt,
        RevokedAt = apiKey.RevokedAt,
        CreatedAt = apiKey.CreatedAt,
        UpdatedAt = apiKey.UpdatedAt,
    };

    private static ApiKeyValidationResult Invalid(string reason) => new()
    {
        IsValid = false,
        FailureReason = reason,
    };

    private async Task TryAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        try
        {
            await auditLogService.LogAsync(auditLog, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Audit logging failed for action {Action}.", auditLog.Action);
        }
    }
}
