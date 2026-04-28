using HookBridge.Application.DTOs.ApiKeys;
using HookBridge.Application.Validation.ApiKeys;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class ApiKeyAllowlistValidationTests
{
    [Fact]
    public async Task Create_WithInvalidAllowlistEntry_FailsValidation()
    {
        var validator = new CreateApiKeyRequestDtoValidator();
        var request = new CreateApiKeyRequestDto
        {
            Name = "Primary",
            AllowedIpAddresses = ["invalid-ip"],
        };

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Update_WithMoreThan50Entries_FailsValidation()
    {
        var validator = new UpdateApiKeyRequestDtoValidator();
        var request = new UpdateApiKeyRequestDto
        {
            AllowedIpAddresses = Enumerable.Range(1, 51).Select(i => $"10.0.0.{i}").ToList(),
        };

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
    }
}
