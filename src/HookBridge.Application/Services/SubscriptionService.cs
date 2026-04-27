using FluentValidation;
using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace HookBridge.Application.Services;

public sealed class SubscriptionService(
    IMongoRepository<Subscription> subscriptionRepository,
    IMongoRepository<Tenant> tenantRepository,
    IGuidGenerator guidGenerator,
    IDateTimeProvider dateTimeProvider,
    IValidator<CreateSubscriptionRequestDto> createValidator,
    ILogger<SubscriptionService> logger) : ISubscriptionService
{
    private const string DefaultBackoffType = "Exponential";
    private const string MaskedValue = "********";

    public async Task<SubscriptionResponseDto> CreateAsync(CreateSubscriptionRequestDto request, CancellationToken cancellationToken = default)
    {
        ApplyDefaults(request);
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var tenant = await tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            throw new KeyNotFoundException($"Tenant '{request.TenantId}' not found.");
        }

        if (tenant.Status != TenantStatus.Active)
        {
            throw new ConflictException($"Tenant '{request.TenantId}' is not active.");
        }

        var now = dateTimeProvider.UtcNow;
        var subscription = new Subscription
        {
            Id = guidGenerator.NewGuid(),
            TenantId = request.TenantId,
            EventType = request.EventType,
            TargetUrl = request.TargetUrl,
            Headers = request.Headers.Select(Map).ToList(),
            Authentication = request.Authentication is null ? null : Map(request.Authentication),
            RetryPolicy = Map(request.RetryPolicy!),
            TimeoutSeconds = request.TimeoutSeconds!.Value,
            IsActive = true,
            DisabledAt = null,
            CreatedAt = now,
            UpdatedAt = null,
        };

        await subscriptionRepository.AddAsync(subscription, cancellationToken);

        logger.LogInformation(
            "Subscription created for TenantId={TenantId}, SubscriptionId={SubscriptionId}, EventType={EventType}, TargetUrl={TargetUrl}",
            subscription.TenantId,
            subscription.Id,
            subscription.EventType,
            subscription.TargetUrl);

        return Map(subscription);
    }

    public async Task<SubscriptionResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var subscription = await subscriptionRepository.GetByIdAsync(id, cancellationToken);
        if (subscription is null)
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

    public async Task<IReadOnlyList<SubscriptionResponseDto>> SearchAsync(SubscriptionSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        var allSubscriptions = await subscriptionRepository.FindAsync(_ => true, cancellationToken);
        var subscriptions = allSubscriptions
            .Where(subscription => string.IsNullOrWhiteSpace(request.TenantId) || subscription.TenantId == request.TenantId)
            .Where(subscription => string.IsNullOrWhiteSpace(request.EventType) || subscription.EventType == request.EventType)
            .Where(subscription => string.IsNullOrWhiteSpace(request.TargetUrl) || subscription.TargetUrl.Contains(request.TargetUrl, StringComparison.OrdinalIgnoreCase))
            .Where(subscription => !request.IsActive.HasValue || subscription.IsActive == request.IsActive.Value)
            .ToList();

        logger.LogInformation(
            "Subscription search executed for TenantId={TenantId}, EventType={EventType}, TargetUrl={TargetUrl}, IsActive={IsActive}, Count={Count}",
            request.TenantId,
            request.EventType,
            request.TargetUrl,
            request.IsActive,
            subscriptions.Count);

        return subscriptions.Select(Map).ToList();
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
            TenantId = subscription.TenantId,
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

    private static AuthenticationConfig Map(AuthenticationDto dto) => new()
    {
        Type = dto.Type,
        Basic = dto.Basic is null ? null : new BasicAuthConfig
        {
            Username = dto.Basic.Username,
            Password = dto.Basic.Password,
        },
        OAuth2 = dto.OAuth2 is null ? null : new OAuth2ClientCredentialsConfig
        {
            TokenUrl = dto.OAuth2.TokenUrl,
            ClientId = dto.OAuth2.ClientId,
            ClientSecret = dto.OAuth2.ClientSecret,
            Scope = dto.OAuth2.Scope,
        },
        ApiKeyHeader = dto.ApiKeyHeader is null ? null : new ApiKeyHeaderConfig
        {
            HeaderName = dto.ApiKeyHeader.HeaderName,
            HeaderValue = dto.ApiKeyHeader.HeaderValue,
        },
        HmacSignature = dto.HmacSignature is null ? null : new HmacSignatureConfig
        {
            Secret = dto.HmacSignature.Secret,
            HeaderName = dto.HmacSignature.HeaderName,
            Algorithm = dto.HmacSignature.Algorithm,
        },
    };

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
}
