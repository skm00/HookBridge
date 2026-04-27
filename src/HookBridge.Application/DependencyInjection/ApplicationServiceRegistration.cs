using FluentValidation;
using HookBridge.Application.DTOs.ApiKeys;
using HookBridge.Application.DTOs.Events;
using HookBridge.Application.DTOs.Tenants;
using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Application.DTOs.Billing;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Services;
using HookBridge.Application.Validation.ApiKeys;
using HookBridge.Application.Validation.Events;
using HookBridge.Application.Validation.Tenants;
using HookBridge.Application.Validation.Subscriptions;
using HookBridge.Application.Validation.Billing;
using HookBridge.Application.DTOs.Auth;
using HookBridge.Application.Validation.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace HookBridge.Application.DependencyInjection;

/// <summary>
/// Registers application-layer services.
/// </summary>
public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IApiKeyService, ApiKeyService>();
        services.AddScoped<IEventIngestionService, EventIngestionService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IDeliveryAttemptService, DeliveryAttemptService>();
        services.AddScoped<IFailedEventService, FailedEventService>();
        services.AddScoped<IWebhookDeliveryService, WebhookDeliveryService>();
        services.AddScoped<IUsageService, UsageService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IRetryPolicyService, RetryPolicyService>();
        services.AddScoped<IValidator<RegisterAdminRequestDto>, RegisterAdminRequestDtoValidator>();
        services.AddScoped<IValidator<LoginRequestDto>, LoginRequestDtoValidator>();
        services.AddScoped<IValidator<CreateTenantRequestDto>, CreateTenantRequestDtoValidator>();
        services.AddScoped<IValidator<UpdateTenantRequestDto>, UpdateTenantRequestDtoValidator>();
        services.AddScoped<IValidator<CreateApiKeyRequestDto>, CreateApiKeyRequestDtoValidator>();
        services.AddScoped<IValidator<EventIngestionRequestDto>, EventIngestionRequestDtoValidator>();
        services.AddScoped<IValidator<CreateSubscriptionRequestDto>, CreateSubscriptionRequestDtoValidator>();
        services.AddScoped<IValidator<UpdateSubscriptionRequestDto>, UpdateSubscriptionRequestDtoValidator>();
        services.AddScoped<IValidator<CreateCheckoutSessionRequestDto>, CreateCheckoutSessionRequestDtoValidator>();

        return services;
    }
}
