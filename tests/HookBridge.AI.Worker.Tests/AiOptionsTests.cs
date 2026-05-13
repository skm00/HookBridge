using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiOptionsTests
{
    [Fact]
    public void Configure_BindsAiOptionsFromConfiguration()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [$"{AiOptions.SectionName}:Enabled"] = "false",
            [$"{AiOptions.SectionName}:Provider"] = "Ollama",
            [$"{AiOptions.SectionName}:Model"] = "llama3.1",
            [$"{AiOptions.SectionName}:Endpoint"] = "http://localhost:11434",
            [$"{AiOptions.SectionName}:TimeoutSeconds"] = "45",
            [$"{AiOptions.SectionName}:MaxRetries"] = "5",
            [$"{AiOptions.SectionName}:SystemPrompt"] = "Analyze webhook failures.",
            [$"{AiOptions.SectionName}:EnablePromptLogging"] = "true",
            [$"{AiOptions.SectionName}:EnableFallback"] = "false",
            [$"{AiOptions.SectionName}:LlmRequestTimeoutSeconds"] = "15",
            [$"{AiOptions.SectionName}:MaxFallbackSummaryLength"] = "750",
            [$"{AiOptions.SectionName}:HealthCheckPrompt"] = "Ready?",
            [$"{AiOptions.SectionName}:MaxPromptPayloadLength"] = "2048",
            [$"{AiOptions.SectionName}:MaskSensitiveValues"] = "false",
            [$"{AiOptions.SectionName}:MaxLogEntriesForSummary"] = "50",
            [$"{AiOptions.SectionName}:MaxLogMessageLength"] = "1024"
        });

        var options = CreateOptions(configuration);

        options.Enabled.Should().BeFalse();
        options.Provider.Should().Be("Ollama");
        options.Model.Should().Be("llama3.1");
        options.Endpoint.Should().Be("http://localhost:11434");
        options.TimeoutSeconds.Should().Be(45);
        options.MaxRetries.Should().Be(5);
        options.SystemPrompt.Should().Be("Analyze webhook failures.");
        options.EnablePromptLogging.Should().BeTrue();
        options.EnableFallback.Should().BeFalse();
        options.LlmRequestTimeoutSeconds.Should().Be(15);
        options.MaxFallbackSummaryLength.Should().Be(750);
        options.HealthCheckPrompt.Should().Be("Ready?");
        options.MaxPromptPayloadLength.Should().Be(2048);
        options.MaskSensitiveValues.Should().BeFalse();
        options.MaxLogEntriesForSummary.Should().Be(50);
        options.MaxLogMessageLength.Should().Be(1024);
    }

    [Fact]
    public void AiOptions_ProvidesSafeDefaults()
    {
        var options = new AiOptions();

        options.Enabled.Should().BeTrue();
        options.Provider.Should().Be("Ollama");
        options.Model.Should().Be("llama3");
        options.Endpoint.Should().Be("http://localhost:11434");
        options.TimeoutSeconds.Should().Be(30);
        options.MaxRetries.Should().Be(3);
        options.SystemPrompt.Should().Be("You are HookBridge AI, an assistant for webhook failure analysis and event processing.");
        options.EnablePromptLogging.Should().BeFalse();
        options.EnableFallback.Should().BeTrue();
        options.LlmRequestTimeoutSeconds.Should().Be(30);
        options.MaxFallbackSummaryLength.Should().Be(1000);
        options.HealthCheckPrompt.Should().Be("Say HookBridge AI is ready");
        options.MaxPromptPayloadLength.Should().Be(4000);
        options.MaskSensitiveValues.Should().BeTrue();
        options.MaxLogEntriesForSummary.Should().Be(100);
        options.MaxLogMessageLength.Should().Be(2000);
    }

    [Fact]
    public void Validate_WithValidAiConfiguration_Succeeds()
    {
        var configuration = BuildConfiguration(ValidEnabledSettings());

        var act = () => CreateOptions(configuration);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenProviderMissingAndAiEnabled_ThrowsOptionsValidationException()
    {
        var settings = ValidEnabledSettings();
        settings[$"{AiOptions.SectionName}:Provider"] = " ";
        var configuration = BuildConfiguration(settings);

        var act = () => CreateOptions(configuration);

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*AI:Provider is required when AI is enabled.*");
    }

    [Fact]
    public void Validate_WhenModelMissingAndAiEnabled_ThrowsOptionsValidationException()
    {
        var settings = ValidEnabledSettings();
        settings[$"{AiOptions.SectionName}:Model"] = " ";
        var configuration = BuildConfiguration(settings);

        var act = () => CreateOptions(configuration);

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*AI:Model is required when AI is enabled.*");
    }

    [Fact]
    public void Validate_WhenEndpointMissingAndAiEnabled_ThrowsOptionsValidationException()
    {
        var settings = ValidEnabledSettings();
        settings[$"{AiOptions.SectionName}:Endpoint"] = " ";
        var configuration = BuildConfiguration(settings);

        var act = () => CreateOptions(configuration);

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*AI:Endpoint is required when AI is enabled.*");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void Validate_WhenTimeoutSecondsInvalid_ThrowsOptionsValidationException(string timeoutSeconds)
    {
        var settings = ValidEnabledSettings();
        settings[$"{AiOptions.SectionName}:TimeoutSeconds"] = timeoutSeconds;
        var configuration = BuildConfiguration(settings);

        var act = () => CreateOptions(configuration);

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*AI:TimeoutSeconds must be greater than 0.*");
    }

    [Fact]
    public void Validate_WhenMaxRetriesInvalid_ThrowsOptionsValidationException()
    {
        var settings = ValidEnabledSettings();
        settings[$"{AiOptions.SectionName}:MaxRetries"] = "-1";
        var configuration = BuildConfiguration(settings);

        var act = () => CreateOptions(configuration);

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*AI:MaxRetries must be 0 or greater.*");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void Validate_WhenLlmRequestTimeoutSecondsInvalid_ThrowsOptionsValidationException(string timeoutSeconds)
    {
        var settings = ValidEnabledSettings();
        settings[$"{AiOptions.SectionName}:LlmRequestTimeoutSeconds"] = timeoutSeconds;
        var configuration = BuildConfiguration(settings);

        var act = () => CreateOptions(configuration);

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*AI:LlmRequestTimeoutSeconds must be greater than 0.*");
    }

    [Fact]
    public void Validate_WhenMaxFallbackSummaryLengthInvalid_ThrowsOptionsValidationException()
    {
        var settings = ValidEnabledSettings();
        settings[$"{AiOptions.SectionName}:MaxFallbackSummaryLength"] = "0";
        var configuration = BuildConfiguration(settings);

        var act = () => CreateOptions(configuration);

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*AI:MaxFallbackSummaryLength must be greater than 0.*");
    }


    [Fact]
    public void Validate_WhenMaxPromptPayloadLengthInvalid_ThrowsOptionsValidationException()
    {
        var settings = ValidEnabledSettings();
        settings[$"{AiOptions.SectionName}:MaxPromptPayloadLength"] = "0";
        var configuration = BuildConfiguration(settings);

        var act = () => CreateOptions(configuration);

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*AI:MaxPromptPayloadLength must be greater than 0.*");
    }

    [Fact]
    public void Validate_WhenMaxLogEntriesForSummaryInvalid_ThrowsOptionsValidationException()
    {
        var settings = ValidEnabledSettings();
        settings[$"{AiOptions.SectionName}:MaxLogEntriesForSummary"] = "0";
        var configuration = BuildConfiguration(settings);

        var act = () => CreateOptions(configuration);

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*AI:MaxLogEntriesForSummary must be greater than 0.*");
    }

    [Fact]
    public void Validate_WhenMaxLogMessageLengthInvalid_ThrowsOptionsValidationException()
    {
        var settings = ValidEnabledSettings();
        settings[$"{AiOptions.SectionName}:MaxLogMessageLength"] = "0";
        var configuration = BuildConfiguration(settings);

        var act = () => CreateOptions(configuration);

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*AI:MaxLogMessageLength must be greater than 0.*");
    }

    [Fact]
    public void Configure_WithEnvironmentVariables_BindsAllAiSettings()
    {
        var previousValues = SetEnvironmentVariables(new Dictionary<string, string?>
        {
            ["AI__Enabled"] = "true",
            ["AI__Provider"] = "Ollama",
            ["AI__Model"] = "llama3.2",
            ["AI__Endpoint"] = "http://ollama:11434",
            ["AI__TimeoutSeconds"] = "60",
            ["AI__MaxRetries"] = "7",
            ["AI__SystemPrompt"] = "Environment prompt.",
            ["AI__EnablePromptLogging"] = "true",
            ["AI__EnableFallback"] = "false",
            ["AI__LlmRequestTimeoutSeconds"] = "20",
            ["AI__MaxFallbackSummaryLength"] = "900",
            ["AI__HealthCheckPrompt"] = "Environment health check",
            ["AI__MaxPromptPayloadLength"] = "1024",
            ["AI__MaskSensitiveValues"] = "true",
            ["AI__MaxLogEntriesForSummary"] = "25",
            ["AI__MaxLogMessageLength"] = "1200"
        });

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            var options = CreateOptions(configuration);

            options.Enabled.Should().BeTrue();
            options.Provider.Should().Be("Ollama");
            options.Model.Should().Be("llama3.2");
            options.Endpoint.Should().Be("http://ollama:11434");
            options.TimeoutSeconds.Should().Be(60);
            options.MaxRetries.Should().Be(7);
            options.SystemPrompt.Should().Be("Environment prompt.");
            options.EnablePromptLogging.Should().BeTrue();
            options.EnableFallback.Should().BeFalse();
            options.LlmRequestTimeoutSeconds.Should().Be(20);
            options.MaxFallbackSummaryLength.Should().Be(900);
            options.HealthCheckPrompt.Should().Be("Environment health check");
            options.MaxPromptPayloadLength.Should().Be(1024);
            options.MaskSensitiveValues.Should().BeTrue();
            options.MaxLogEntriesForSummary.Should().Be(25);
            options.MaxLogMessageLength.Should().Be(1200);
        }
        finally
        {
            SetEnvironmentVariables(previousValues);
        }
    }

    [Fact]
    public void ProductionAppsettings_DisablesAiByDefaultAndPassesValidation()
    {
        var configuration = BuildJsonConfiguration("appsettings.json", "appsettings.Production.json");

        var options = CreateOptions(configuration);

        options.Enabled.Should().BeFalse();
        options.Provider.Should().BeEmpty();
        options.Model.Should().BeEmpty();
        options.Endpoint.Should().BeEmpty();
        options.EnablePromptLogging.Should().BeFalse();
        options.EnableFallback.Should().BeTrue();
        options.LlmRequestTimeoutSeconds.Should().Be(30);
        options.MaxFallbackSummaryLength.Should().Be(1000);
        options.TimeoutSeconds.Should().Be(30);
        options.MaxRetries.Should().Be(3);
        options.MaxPromptPayloadLength.Should().Be(4000);
        options.MaskSensitiveValues.Should().BeTrue();
        options.MaxLogEntriesForSummary.Should().Be(100);
        options.MaxLogMessageLength.Should().Be(2000);
    }

    [Fact]
    public void DevelopmentAppsettings_UsesLocalOllamaDefaults()
    {
        var configuration = BuildJsonConfiguration("appsettings.json", "appsettings.Development.json");

        var options = CreateOptions(configuration);

        options.Enabled.Should().BeTrue();
        options.Provider.Should().Be("Ollama");
        options.Model.Should().Be("llama3");
        options.Endpoint.Should().Be("http://localhost:11434");
        options.TimeoutSeconds.Should().Be(30);
        options.MaxRetries.Should().Be(3);
        options.EnablePromptLogging.Should().BeFalse();
        options.EnableFallback.Should().BeTrue();
        options.LlmRequestTimeoutSeconds.Should().Be(30);
        options.MaxFallbackSummaryLength.Should().Be(1000);
        options.HealthCheckPrompt.Should().Be("Say HookBridge AI is ready");
        options.MaxPromptPayloadLength.Should().Be(4000);
        options.MaskSensitiveValues.Should().BeTrue();
        options.MaxLogEntriesForSummary.Should().Be(100);
        options.MaxLogMessageLength.Should().Be(2000);
    }

    private static Dictionary<string, string?> ValidEnabledSettings()
    {
        return new Dictionary<string, string?>
        {
            [$"{AiOptions.SectionName}:Enabled"] = "true",
            [$"{AiOptions.SectionName}:Provider"] = "Ollama",
            [$"{AiOptions.SectionName}:Model"] = "llama3",
            [$"{AiOptions.SectionName}:Endpoint"] = "http://localhost:11434",
            [$"{AiOptions.SectionName}:TimeoutSeconds"] = "30",
            [$"{AiOptions.SectionName}:MaxRetries"] = "3",
            [$"{AiOptions.SectionName}:SystemPrompt"] = "You are HookBridge AI, an assistant for webhook failure analysis and event processing.",
            [$"{AiOptions.SectionName}:EnablePromptLogging"] = "false",
            [$"{AiOptions.SectionName}:HealthCheckPrompt"] = "Say HookBridge AI is ready",
            [$"{AiOptions.SectionName}:MaxPromptPayloadLength"] = "4000",
            [$"{AiOptions.SectionName}:MaskSensitiveValues"] = "true"
        };
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> settings)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    private static IConfiguration BuildJsonConfiguration(params string[] files)
    {
        var workerDirectory = FindRepositoryRoot().Combine("src", "HookBridge.AI.Worker");
        var builder = new ConfigurationBuilder().SetBasePath(workerDirectory.FullName);

        foreach (var file in files)
        {
            builder.AddJsonFile(file, optional: false, reloadOnChange: false);
        }

        return builder.Build();
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HookBridge.sln")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new DirectoryNotFoundException("Could not find repository root containing HookBridge.sln.");
    }

    private static AiOptions CreateOptions(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddAiOptions(configuration);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<AiOptions>>().Value;
    }

    private static Dictionary<string, string?> SetEnvironmentVariables(Dictionary<string, string?> values)
    {
        var previousValues = new Dictionary<string, string?>();

        foreach (var (key, value) in values)
        {
            previousValues[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }

        return previousValues;
    }
}

internal static class DirectoryInfoExtensions
{
    public static DirectoryInfo Combine(this DirectoryInfo directory, params string[] paths)
    {
        return new DirectoryInfo(Path.Combine(new[] { directory.FullName }.Concat(paths).ToArray()));
    }
}
