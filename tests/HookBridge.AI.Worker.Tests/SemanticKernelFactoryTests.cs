using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace HookBridge.AI.Worker.Tests;

public sealed class SemanticKernelFactoryTests
{
    [Fact]
    public void AddAiKernelServices_RegistersKernelFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.Configure<AiOptions>(_ => { });

        services.AddAiKernelServices();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IKernelFactory>();

        factory.Should().BeOfType<SemanticKernelFactory>();
    }

    [Fact]
    public void CreateKernel_WhenEndpointMissing_ThrowsMeaningfulExceptionAndLogsError()
    {
        var logger = new TestLogger<SemanticKernelFactory>();
        var factory = CreateFactory(new AiOptions { Endpoint = "" }, logger);

        var act = () => factory.CreateKernel();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AI endpoint configuration is missing*");
        logger.Records.Should().Contain(record =>
            record.Level == LogLevel.Error &&
            record.Message.Contains("AI endpoint configuration is missing", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateKernel_WhenModelMissing_ThrowsMeaningfulExceptionAndLogsError()
    {
        var logger = new TestLogger<SemanticKernelFactory>();
        var factory = CreateFactory(new AiOptions { Model = " " }, logger);

        var act = () => factory.CreateKernel();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AI model configuration is missing*");
        logger.Records.Should().Contain(record =>
            record.Level == LogLevel.Error &&
            record.Message.Contains("AI model configuration is missing", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateKernel_WithValidOllamaConfiguration_ReturnsKernelAndLogsSuccess()
    {
        var logger = new TestLogger<SemanticKernelFactory>();
        var options = new AiOptions
        {
            Provider = "Ollama",
            Model = "llama3.1",
            Endpoint = "http://localhost:11434"
        };
        var factory = CreateFactory(options, logger);

        var kernel = factory.CreateKernel();

        kernel.Should().BeOfType<Kernel>();
        logger.Records.Should().Contain(record =>
            record.Level == LogLevel.Information &&
            record.Message.Contains("Semantic Kernel created successfully", StringComparison.Ordinal));
    }

    private static SemanticKernelFactory CreateFactory(
        AiOptions options,
        TestLogger<SemanticKernelFactory> logger)
    {
        return new SemanticKernelFactory(Options.Create(options), logger);
    }
}
