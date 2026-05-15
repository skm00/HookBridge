using FluentAssertions;
using HookBridge.AI.Worker.PromptVersioning;
using HookBridge.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HookBridge.Api.Tests;

public sealed class AiPromptsControllerTests
{
    [Fact]
    public void ListPromptVersions_ReturnsMetadataList()
    {
        var provider = new Mock<IAiPromptVersionProvider>();
        provider.Setup(item => item.ListPromptVersions(false)).Returns([
            new AiPromptVersionInfoDto { PromptName = AiPromptNames.WebhookFailureAnalysis, Version = "v1.0.0", Hash = "sha256:abc" }
        ]);
        var controller = new AiPromptsController(provider.Object, NullLogger<AiPromptsController>.Instance);

        var result = controller.ListPromptVersions().Result.Should().BeOfType<OkObjectResult>().Subject;

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public void GetPromptVersion_ReturnsMetadata()
    {
        var metadata = new AiPromptVersionInfoDto { PromptName = AiPromptNames.AiLogSummary, Version = "v1.0.0", Hash = "sha256:abc" };
        var provider = new Mock<IAiPromptVersionProvider>();
        provider.Setup(item => item.GetPromptMetadata(AiPromptNames.AiLogSummary, "v1.0.0", false)).Returns(metadata);
        var controller = new AiPromptsController(provider.Object, NullLogger<AiPromptsController>.Instance);

        var result = controller.GetPromptVersion(AiPromptNames.AiLogSummary, "v1.0.0").Result.Should().BeOfType<OkObjectResult>().Subject;

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public void GetPromptVersion_ReturnsBadRequestForInvalidVersion()
    {
        var controller = new AiPromptsController(Mock.Of<IAiPromptVersionProvider>(), NullLogger<AiPromptsController>.Instance);

        var result = controller.GetPromptVersion(AiPromptNames.AiLogSummary, "1.0.0").Result.Should().BeOfType<ObjectResult>().Subject;

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void GetPromptVersion_ReturnsNotFoundForMissingPrompt()
    {
        var provider = new Mock<IAiPromptVersionProvider>();
        provider.Setup(item => item.GetPromptMetadata(AiPromptNames.AiLogSummary, "v9.9.9", false)).Throws(new FileNotFoundException());
        var controller = new AiPromptsController(provider.Object, NullLogger<AiPromptsController>.Instance);

        var result = controller.GetPromptVersion(AiPromptNames.AiLogSummary, "v9.9.9").Result.Should().BeOfType<ObjectResult>().Subject;

        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
