using HookBridge.Application.Configuration;
using HookBridge.Application.DTOs.EndpointValidation;
using HookBridge.Application.Validation.EndpointValidation;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class EndpointValidationRequestDtoValidatorTests
{
    [Fact]
    public async Task InvalidUrl_FailsValidation()
    {
        var validator = CreateValidator(isProduction: false, allowPrivate: false);
        var result = await validator.ValidateAsync(new EndpointValidationRequestDto { TargetUrl = "not-url", TimeoutSeconds = 10 });
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task TimeoutOutOfRange_FailsValidation()
    {
        var validator = CreateValidator(isProduction: false, allowPrivate: false);
        var result = await validator.ValidateAsync(new EndpointValidationRequestDto { TargetUrl = "https://example.com", TimeoutSeconds = 31 });
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task LocalhostInProduction_FailsValidation()
    {
        var validator = CreateValidator(isProduction: true, allowPrivate: false);
        var result = await validator.ValidateAsync(new EndpointValidationRequestDto { TargetUrl = "http://localhost:5000/hook", TimeoutSeconds = 10 });
        Assert.False(result.IsValid);
    }

    private static EndpointValidationRequestDtoValidator CreateValidator(bool isProduction, bool allowPrivate)
        => new(Options.Create(new SecuritySettings { AllowPrivateNetworkTargetUrls = allowPrivate }), new FakeHostEnvironment(isProduction));

    private sealed class FakeHostEnvironment(bool isProduction) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = isProduction ? Environments.Production : Environments.Development;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
