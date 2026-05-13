using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace HookBridge.AI.Worker.Services;

public sealed class SemanticKernelFactory : IKernelFactory
{
    private const string OllamaProvider = "Ollama";

    private readonly ILogger<SemanticKernelFactory> _logger;
    private readonly IOptions<AiOptions> _options;

    public SemanticKernelFactory(
        IOptions<AiOptions> options,
        ILogger<SemanticKernelFactory> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Kernel CreateKernel()
    {
        var options = _options.Value;
        ValidateOptions(options);

        var endpoint = new Uri(options.Endpoint, UriKind.Absolute);
        var builder = Kernel.CreateBuilder();

#pragma warning disable SKEXP0070 // Ollama connector is currently experimental in Semantic Kernel.
        builder.AddOllamaChatCompletion(options.Model, endpoint, serviceId: options.Provider);
#pragma warning restore SKEXP0070

        var kernel = builder.Build();

        _logger.LogInformation(
            "Semantic Kernel created successfully for AI provider {Provider}, model {Model}, endpoint {Endpoint}.",
            options.Provider,
            options.Model,
            options.Endpoint);

        return kernel;
    }

    private void ValidateOptions(AiOptions options)
    {
        if (!string.Equals(options.Provider, OllamaProvider, StringComparison.OrdinalIgnoreCase))
        {
            throw LogAndCreateConfigurationException(
                "AI provider {Provider} is not supported. Supported provider: Ollama.",
                $"AI provider '{options.Provider}' is not supported. Supported provider: Ollama.",
                options.Provider);
        }

        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            throw LogAndCreateConfigurationException(
                "AI endpoint configuration is missing. Set AI:Endpoint to the Ollama endpoint URL.",
                "AI endpoint configuration is missing. Set AI:Endpoint to the Ollama endpoint URL.");
        }

        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw LogAndCreateConfigurationException(
                "AI endpoint configuration is invalid. Set AI:Endpoint to an absolute HTTP or HTTPS URL.",
                "AI endpoint configuration is invalid. Set AI:Endpoint to an absolute HTTP or HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw LogAndCreateConfigurationException(
                "AI model configuration is missing. Set AI:Model to an Ollama model name.",
                "AI model configuration is missing. Set AI:Model to an Ollama model name.");
        }
    }

    private InvalidOperationException LogAndCreateConfigurationException(
        string logMessage,
        string exceptionMessage,
        params object?[] args)
    {
        _logger.LogError(logMessage, args);
        return new InvalidOperationException(exceptionMessage);
    }
}
