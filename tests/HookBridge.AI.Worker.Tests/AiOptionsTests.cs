using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiOptionsTests
{
    [Fact]
    public void Configure_BindsAiOptionsFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{AiOptions.SectionName}:Enabled"] = "false",
                [$"{AiOptions.SectionName}:Provider"] = "Ollama",
                [$"{AiOptions.SectionName}:Model"] = "llama3.1",
                [$"{AiOptions.SectionName}:Endpoint"] = "http://localhost:11434"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AiOptions>>().Value;

        options.Enabled.Should().BeFalse();
        options.Provider.Should().Be("Ollama");
        options.Model.Should().Be("llama3.1");
        options.Endpoint.Should().Be("http://localhost:11434");
    }

    [Fact]
    public void AiOptions_ProvidesOllamaDefaults()
    {
        var options = new AiOptions();

        options.Enabled.Should().BeTrue();
        options.Provider.Should().Be("Ollama");
        options.Model.Should().Be("llama3");
        options.Endpoint.Should().Be("http://localhost:11434");
    }
}
