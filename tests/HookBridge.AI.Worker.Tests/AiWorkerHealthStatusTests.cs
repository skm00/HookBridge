using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.Health;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiWorkerHealthStatusTests
{
    [Fact]
    public void GetStatus_ReturnsHealthyWhenAiConfigurationIsUsable()
    {
        var healthStatus = new AiWorkerHealthStatus(Options.Create(new AiOptions()));

        var status = healthStatus.GetStatus();

        status.IsHealthy.Should().BeTrue();
        status.Enabled.Should().BeTrue();
        status.Provider.Should().Be("Ollama");
        status.Model.Should().Be("llama3");
        status.Endpoint.Should().Be("http://localhost:11434");
    }

    [Fact]
    public void GetStatus_ReturnsUnhealthyWhenEndpointIsInvalid()
    {
        var healthStatus = new AiWorkerHealthStatus(Options.Create(new AiOptions
        {
            Endpoint = "not-a-url"
        }));

        var status = healthStatus.GetStatus();

        status.IsHealthy.Should().BeFalse();
    }
}
