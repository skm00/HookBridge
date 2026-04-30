using FluentValidation;
using HookBridge.Application.DTOs.Common;
using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace HookBridge.Application.Services;

public sealed class SubscriptionService(
    IMongoRepository<Subscription> subscriptionRepository,
    IMongoRepository<Tenant> tenantRepository,
    IAuditLogService auditLogService,
    IGuidGenerator guidGenerator,
    IDateTimeProvider dateTimeProvider,
    IValidator<CreateSubscriptionRequestDto> createValidator,
    IValidator<UpdateSubscriptionRequestDto> updateValidator,
    ISecretEncryptionService secretEncryptionService,
    ILogger<SubscriptionService> logger) : ISubscriptionService
{
    private const string DefaultBackoffType = "Exponential";
    private const string MaskedValue = "********";

    public async Task<SubscriptionResponseDto> CreateAsync(string tenantId, CreateSubscriptionRequestDto request, CancellationToken cancellationToken = default)
    {
        ApplyDefaults(request);
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

        var now = dateTimeProvider.UtcNow;
        var subscription = new Subscription
        {
            Id = guidGenerator.NewGuid(),
            TenantId = tenantId,
            EventType = string.IsNullOrWhiteSpace(request.EventType) ? "*" : request.EventType,
            TargetUrl = request.TargetUrl,
            Headers = request.Headers.Select(Map).ToList(),
            Authentication = request.Authentication is null ? null : MapAndEncrypt(request.Authentication, null, secretEncryptionService),
            RetryPolicy = Map(request.RetryPolicy!),
            TimeoutSeconds = request.TimeoutSeconds!.Value,
            IsActive = true,
            DisabledAt = null,
            CreatedAt = now,
            UpdatedAt = null,
        };

        await subscriptionRepository.AddAsync(subscription, cancellationToken);
        await TryAuditAsync(
            new AuditLog
            {
                    Action = "SubscriptionCreated",
                ResourceType = nameof(Subscription),
                ResourceId = subscription.Id,
                Description = $"Subscription created for event '{subscription.EventType}'.",
                Metadata = BuildSubscriptionMetadata(subscription),
            },
            cancellationToken);

        logger.LogInformation(
            "Subscription created for TenantId={TenantId}, SubscriptionId={SubscriptionId}, EventType={EventType}, TargetUrl={TargetUrl}",
            subscription.TenantId,
            subscription.Id,
            subscription.EventType,
            subscription.TargetUrl);

        return Map(subscription);
    }

    public async Task<SubscriptionResponseDto?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        var subscription = await subscriptionRepository.GetByIdAsync(id, cancellationToken);
        if (subscription is null || !string.Equals(subscription.TenantId, tenantId, StringComparison.Ordinal))
        {
            return null;
        }

        logger.LogInformation(
            "Subscription fetched by id for TenantId={TenantId}, SubscriptionId={SubscriptionId}, EventType={EventType}, TargetUrl={TargetUrl}",
            subscription.TenantId,
            subscription.Id,
            subscription.EventType,
            subscription.TargetUrl);

        return Map(subscription);
    }

    public async Task<PagedResponseDto<SubscriptionResponseDto>> SearchAsync(SubscriptionSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        var descending = request.NormalizedSortDirection == "desc";

        var subscriptions = await subscriptionRepository.QueryAsync(
            subscription =>
                subscription.TenantId == request.TenantId
                && (string.IsNullOrWhiteSpace(request.EventType) || subscription.EventType == request.EventType)
                && (string.IsNullOrWhiteSpace(request.TargetUrl) || subscription.TargetUrl.Contains(request.TargetUrl))
                && (!request.IsActive.HasValue || subscription.IsActive == request.IsActive.Value),
            GetSortDefinition(request.SortBy, descending),
            request.Skip,
            pageSize,
            cancellationToken);

        logger.LogInformation(
            "Subscription search executed for TenantId={TenantId}, EventType={EventType}, TargetUrl={TargetUrl}, IsActive={IsActive}, Count={Count}",
            request.TenantId,
            request.EventType,
            request.TargetUrl,
            request.IsActive,
            subscriptions.TotalCount);

        return PagedResponseDto<SubscriptionResponseDto>.Create(subscriptions.Items.Select(Map).ToList(), pageNumber, pageSize, subscriptions.TotalCount);
    }

    private static SortDefinition<Subscription> GetSortDefinition(string? sortBy, bool descending)
    {
        var sortBuilder = Builders<Subscription>.Sort;
        return (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "createdat" => descending ? sortBuilder.Descending(x => x.CreatedAt) : sortBuilder.Ascending(x => x.CreatedAt),
            "eventtype" => descending ? sortBuilder.Descending(x => x.EventType) : sortBuilder.Ascending(x => x.EventType),
            "targeturl" => descending ? sortBuilder.Descending(x => x.TargetUrl) : sortBuilder.Ascending(x => x.TargetUrl),
            "isactive" => descending ? sortBuilder.Descending(x => x.IsActive) : sortBuilder.Ascending(x => x.IsActive),
            _ => sortBuilder.Descending(x => x.CreatedAt),
        };
    }

    public async Task<SubscriptionResponseDto?> UpdateAsync(string tenantId, string id, UpdateSubscriptionRequestDto request, CancellationToken cancellationToken = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var subscription = await subscriptionRepository.GetByIdAsync(id, cancellationToken);
        if (subscription is null || !string.Equals(subscription.TenantId, tenantId, StringComparison.Ordinal))
        {
            return null;
        }

        if (request.EventType is not null)
        {
            subscription.EventType = string.IsNullOrWhiteSpace(request.EventType) ? "*" : request.EventType;
        }

        if (request.TargetUrl is not null)
        {
            subscription.TargetUrl = request.TargetUrl;
        }

        if (request.Headers is not null)
        {
            subscription.Headers = request.Headers.Select(Map).ToList();
        }

        if (request.Authentication is not null)
        {
            subscription.Authentication = MapAndEncrypt(request.Authentication, subscription.Authentication, secretEncryptionService);
        }

        if (request.RetryPolicy is not null)
        {
            subscription.RetryPolicy = Map(request.RetryPolicy);
        }

        if (request.TimeoutSeconds.HasValue)
        {
            subscription.TimeoutSeconds = request.TimeoutSeconds.Value;
        }

        subscription.UpdatedAt = dateTimeProvider.UtcNow;

        await subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        await TryAuditAsync(
            new AuditLog
            {
                    Action = "SubscriptionUpdated",
                ResourceType = nameof(Subscription),
                ResourceId = subscription.Id,
                Description = $"Subscription '{subscription.Id}' updated.",
                Metadata = BuildSubscriptionMetadata(subscription),
            },
            cancellationToken);

        logger.LogInformation(
            "Subscription updated SubscriptionId={SubscriptionId} TenantId={TenantId} EventType={EventType}",
            subscription.Id,
            subscription.TenantId,
            subscription.EventType);

        return Map(subscription);
    }

    public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        var subscription = await subscriptionRepository.GetByIdAsync(id, cancellationToken);
        if (subscription is null || !string.Equals(subscription.TenantId, tenantId, StringComparison.Ordinal))
        {
            return false;
        }

        await subscriptionRepository.DeleteAsync(id, cancellationToken);
        await TryAuditAsync(
            new AuditLog
            {
                    Action = "SubscriptionDeleted",
                ResourceType = nameof(Subscription),
                ResourceId = subscription.Id,
                Description = $"Subscription '{subscription.Id}' deleted.",
                Metadata = new Dictionary<string, object?>
                {
                    ["eventType"] = subscription.EventType,
                    ["targetUrl"] = subscription.TargetUrl,
                },
            },
            cancellationToken);

        logger.LogInformation(
            "Subscription deleted SubscriptionId={SubscriptionId} TenantId={TenantId} EventType={EventType}",
            subscription.Id,
            subscription.TenantId,
            subscription.EventType);

        return true;
    }

    public async Task<bool> EnableAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        var subscription = await subscriptionRepository.GetByIdAsync(id, cancellationToken);
        if (subscription is null || !string.Equals(subscription.TenantId, tenantId, StringComparison.Ordinal))
        {
            return false;
        }

        subscription.IsActive = true;
        subscription.DisabledAt = null;
        subscription.UpdatedAt = dateTimeProvider.UtcNow;

        await subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        await TryAuditAsync(
            new AuditLog
            {
                    Action = "SubscriptionEnabled",
                ResourceType = nameof(Subscription),
                ResourceId = subscription.Id,
                Description = $"Subscription '{subscription.Id}' enabled.",
                Metadata = BuildSubscriptionMetadata(subscription),
            },
            cancellationToken);

        logger.LogInformation(
            "Subscription enabled SubscriptionId={SubscriptionId} TenantId={TenantId} EventType={EventType}",
            subscription.Id,
            subscription.TenantId,
            subscription.EventType);

        return true;
    }

    public async Task<bool> DisableAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        var subscription = await subscriptionRepository.GetByIdAsync(id, cancellationToken);
        if (subscription is null)
        {
            return false;
        }

        var now = dateTimeProvider.UtcNow;
        subscription.IsActive = false;
        subscription.DisabledAt = now;
        subscription.UpdatedAt = now;

        await subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        await TryAuditAsync(
            new AuditLog
            {
                    Action = "SubscriptionDisabled",
                ResourceType = nameof(Subscription),
                ResourceId = subscription.Id,
                Description = $"Subscription '{subscription.Id}' disabled.",
                Metadata = BuildSubscriptionMetadata(subscription),
            },
            cancellationToken);

        logger.LogInformation(
            "Subscription disabled SubscriptionId={SubscriptionId} TenantId={TenantId} EventType={EventType}",
            subscription.Id,
            subscription.TenantId,
            subscription.EventType);

        return true;
    }

    private static void ApplyDefaults(CreateSubscriptionRequestDto request)
    {
        request.TimeoutSeconds ??= 30;
        request.RetryPolicy ??= new RetryPolicyDto
        {
            MaxAttempts = 3,
            InitialDelaySeconds = 30,
            BackoffType = DefaultBackoffType,
        };
    }

    private static SubscriptionResponseDto Map(Subscription subscription)
        => new()
        {
            Id = subscription.Id,
            EventType = subscription.EventType,
            TargetUrl = subscription.TargetUrl,
            Headers = subscription.Headers.Select(Map).ToList(),
            Authentication = subscription.Authentication is null ? null : MapAndMask(subscription.Authentication),
            RetryPolicy = Map(subscription.RetryPolicy),
            TimeoutSeconds = subscription.TimeoutSeconds,
            IsActive = subscription.IsActive,
            DisabledAt = subscription.DisabledAt,
            CreatedAt = subscription.CreatedAt,
            UpdatedAt = subscription.UpdatedAt,
        };

    private static KeyValueItem Map(KeyValueDto dto) => new()
    {
        Name = dto.Name,
        Value = dto.Value,
    };

    private static KeyValueDto Map(KeyValueItem item) => new()
    {
        Name = item.Name,
        Value = item.Value,
    };

    private static RetryPolicy Map(RetryPolicyDto dto) => new()
    {
        MaxAttempts = dto.MaxAttempts,
        InitialDelaySeconds = dto.InitialDelaySeconds,
        BackoffType = dto.BackoffType,
    };

    private static RetryPolicyDto Map(RetryPolicy policy) => new()
    {
        MaxAttempts = policy.MaxAttempts,
        InitialDelaySeconds = policy.InitialDelaySeconds,
        BackoffType = policy.BackoffType,
    };

    private static AuthenticationConfig MapAndEncrypt(
        AuthenticationDto dto,
        AuthenticationConfig? existingConfig,
        ISecretEncryptionService secretEncryptionService)
    {
        var sameTypeAsExisting = existingConfig is not null
            && dto.Type.Equals(existingConfig.Type, StringComparison.Ordinal);

        return new AuthenticationConfig
        {
            Type = dto.Type,
            Basic = dto.Basic is null ? null : new BasicAuthConfig
            {
                Username = dto.Basic.Username,
                Password = EncryptOrPreserveMasked(
                    dto.Basic.Password,
                    sameTypeAsExisting ? existingConfig?.Basic?.Password : null,
                    secretEncryptionService),
            },
            OAuth2 = dto.OAuth2 is null ? null : new OAuth2ClientCredentialsConfig
            {
                TokenUrl = dto.OAuth2.TokenUrl,
                ClientId = dto.OAuth2.ClientId,
                ClientSecret = EncryptOrPreserveMasked(
                    dto.OAuth2.ClientSecret,
                    sameTypeAsExisting ? existingConfig?.OAuth2?.ClientSecret : null,
                    secretEncryptionService),
                Scope = dto.OAuth2.Scope,
            },
            ApiKeyHeader = dto.ApiKeyHeader is null ? null : new ApiKeyHeaderConfig
            {
                HeaderName = dto.ApiKeyHeader.HeaderName,
                HeaderValue = EncryptOrPreserveMasked(
                    dto.ApiKeyHeader.HeaderValue,
                    sameTypeAsExisting ? existingConfig?.ApiKeyHeader?.HeaderValue : null,
                    secretEncryptionService),
            },
            HmacSignature = dto.HmacSignature is null ? null : new HmacSignatureConfig
            {
                Secret = EncryptOrPreserveMasked(
                    dto.HmacSignature.Secret,
                    sameTypeAsExisting ? existingConfig?.HmacSignature?.Secret : null,
                    secretEncryptionService),
                HeaderName = dto.HmacSignature.HeaderName,
                Algorithm = dto.HmacSignature.Algorithm,
            },
        };
    }

    private static string EncryptOrPreserveMasked(
        string value,
        string? existingEncryptedValue,
        ISecretEncryptionService secretEncryptionService)
    {
        if (value == MaskedValue && !string.IsNullOrWhiteSpace(existingEncryptedValue))
        {
            return existingEncryptedValue;
        }

        return secretEncryptionService.IsEncrypted(value)
            ? value
            : secretEncryptionService.Encrypt(value);
    }

    private static AuthenticationDto MapAndMask(AuthenticationConfig config) => new()
    {
        Type = config.Type,
        Basic = config.Basic is null ? null : new BasicAuthDto
        {
            Username = config.Basic.Username,
            Password = MaskedValue,
        },
        OAuth2 = config.OAuth2 is null ? null : new OAuth2ClientCredentialsDto
        {
            TokenUrl = config.OAuth2.TokenUrl,
            ClientId = config.OAuth2.ClientId,
            ClientSecret = MaskedValue,
            Scope = config.OAuth2.Scope,
        },
        ApiKeyHeader = config.ApiKeyHeader is null ? null : new ApiKeyHeaderDto
        {
            HeaderName = config.ApiKeyHeader.HeaderName,
            HeaderValue = MaskedValue,
        },
        HmacSignature = config.HmacSignature is null ? null : new HmacSignatureDto
        {
            Secret = MaskedValue,
            HeaderName = config.HmacSignature.HeaderName,
            Algorithm = config.HmacSignature.Algorithm,
        },
    };

    private static IReadOnlyDictionary<string, object?> BuildSubscriptionMetadata(Subscription subscription)
        => new Dictionary<string, object?>
        {
            ["eventType"] = subscription.EventType,
            ["targetUrl"] = subscription.TargetUrl,
            ["isActive"] = subscription.IsActive,
            ["timeoutSeconds"] = subscription.TimeoutSeconds,
            ["retryMaxAttempts"] = subscription.RetryPolicy.MaxAttempts,
            ["headersCount"] = subscription.Headers.Count,
            ["authenticationType"] = subscription.Authentication?.Type,
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
