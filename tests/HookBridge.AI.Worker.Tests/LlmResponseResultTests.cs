using FluentAssertions;
using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Tests;

public sealed class LlmResponseResultTests
{
    [Fact]
    public void Success_CreatesSuccessfulResultWithoutFallbackReason()
    {
        var result = LlmResponseResult.Success("{\"ok\":true}", 12);

        result.IsSuccess.Should().BeTrue();
        result.ResponseText.Should().Be("{\"ok\":true}");
        result.FallbackReason.Should().Be(AiFallbackReason.None);
        result.DurationMs.Should().Be(12);
    }

    [Theory]
    [InlineData(AiFallbackReason.ProviderUnavailable)]
    [InlineData(AiFallbackReason.Timeout)]
    [InlineData(AiFallbackReason.InvalidJson)]
    [InlineData(AiFallbackReason.InvalidResponse)]
    [InlineData(AiFallbackReason.ModelUnavailable)]
    public void Failure_CreatesFailureResultWithReason(AiFallbackReason reason)
    {
        var result = LlmResponseResult.Failure(reason, "safe error", 34, 503);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("safe error");
        result.FallbackReason.Should().Be(reason);
        result.StatusCode.Should().Be(503);
        result.DurationMs.Should().Be(34);
    }
}
