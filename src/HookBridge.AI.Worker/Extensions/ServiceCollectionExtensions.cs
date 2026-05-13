using HookBridge.AI.Worker.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HookBridge.AI.Worker.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiKernelServices(this IServiceCollection services)
    {
        services.AddSingleton<IKernelFactory, SemanticKernelFactory>();
        return services;
    }
}
