using FluentValidation;
using HookBridge.Application.DTOs.ApiKeys;
using HookBridge.Application.DTOs.Events;
using HookBridge.Application.DTOs.Tenants;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Services;
using HookBridge.Application.Validation.ApiKeys;
using HookBridge.Application.Validation.Events;
using HookBridge.Application.Validation.Tenants;
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
        services.AddScoped<IValidator<CreateTenantRequestDto>, CreateTenantRequestDtoValidator>();
        services.AddScoped<IValidator<UpdateTenantRequestDto>, UpdateTenantRequestDtoValidator>();
        services.AddScoped<IValidator<CreateApiKeyRequestDto>, CreateApiKeyRequestDtoValidator>();
        services.AddScoped<IValidator<EventIngestionRequestDto>, EventIngestionRequestDtoValidator>();

        return services;
    }
}
