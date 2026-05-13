using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HookBridge.AI.Worker.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Provider),
                "AI:Provider is required when AI is enabled.")
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Model),
                "AI:Model is required when AI is enabled.")
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Endpoint),
                "AI:Endpoint is required when AI is enabled.")
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddAiKernelServices(this IServiceCollection services)
    {
        services.AddSingleton<IKernelFactory, SemanticKernelFactory>();
        return services;
    }
}
