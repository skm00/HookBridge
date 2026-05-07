using FluentAssertions;
using FluentValidation.TestHelper;
using HookBridge.Application.Configuration;
using HookBridge.Application.DTOs.Auth;
using HookBridge.Application.DTOs.Billing;
using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Application.DTOs.Tenants;
using HookBridge.Application.Validation.Auth;
using HookBridge.Application.Validation.Billing;
using HookBridge.Application.Validation.Subscriptions;
using HookBridge.Application.Validation.Tenants;
using HookBridge.Domain.Enums;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HookBridge.Application.Tests;

public sealed class AdditionalValidationCoverageTests
{
    [Fact]
    public void Validate_WhenLoginIsMissingPassword_ShouldHaveValidationError()
    {
        var validator = new LoginRequestDtoValidator();

        var result = validator.TestValidate(new LoginRequestDto { Email = "admin@example.com", Password = "" });

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_WhenRegisterAdminHasWeakPassword_ShouldHaveValidationError()
    {
        var validator = new RegisterAdminRequestDtoValidator();

        var result = validator.TestValidate(new RegisterAdminRequestDto { Email = "admin@example.com", Password = "short" });

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_WhenRegisterAdminHasInvalidEmail_ShouldHaveValidationError()
    {
        var validator = new RegisterAdminRequestDtoValidator();

        var result = validator.TestValidate(new RegisterAdminRequestDto { Email = "not-an-email", Password = "StrongPassword123!" });

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_WhenTenantSlugContainsUppercaseCharacters_ShouldHaveValidationError()
    {
        var validator = new CreateTenantRequestDtoValidator();

        var result = validator.TestValidate(new CreateTenantRequestDto
        {
            Name = "Acme",
            Slug = "Acme-Corp",
            ContactEmail = "owner@acme.example",
        });

        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    [Fact]
    public void Validate_WhenTenantHasTooManyNotificationEmails_ShouldHaveValidationError()
    {
        var validator = new CreateTenantRequestDtoValidator();

        var result = validator.TestValidate(new CreateTenantRequestDto
        {
            Name = "Acme",
            Slug = "acme",
            NotificationEmails = Enumerable.Range(0, 11).Select(i => $"ops{i}@acme.example").ToList(),
        });

        result.ShouldHaveValidationErrorFor(x => x.NotificationEmails);
    }

    [Fact]
    public void Validate_WhenCheckoutPlanIsFree_ShouldHaveValidationError()
    {
        var validator = new CreateCheckoutSessionRequestDtoValidator();

        var result = validator.TestValidate(new CreateCheckoutSessionRequestDto { Plan = BillingPlan.Free });

        result.ShouldHaveValidationErrorFor(x => x.Plan);
    }

    [Fact]
    public void Validate_WhenCreateSubscriptionSetsDuplicateHeaders_ShouldHaveValidationError()
    {
        var validator = new CreateSubscriptionRequestDtoValidator();
        var request = CreateSubscriptionRequest();
        request.Headers =
        [
            new KeyValueDto { Name = "x-hook", Value = "one" },
            new KeyValueDto { Name = "X-Hook", Value = "two" },
        ];

        var result = validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Headers);
    }

    [Fact]
    public void Validate_WhenCreateSubscriptionSetsAuthorizationHeaderWithBasicAuth_ShouldHaveValidationError()
    {
        var validator = new CreateSubscriptionRequestDtoValidator();
        var request = CreateSubscriptionRequest();
        request.Headers = [new KeyValueDto { Name = "Authorization", Value = "Bearer user-supplied" }];
        request.Authentication = new AuthenticationDto
        {
            Type = "Basic",
            Basic = new BasicAuthDto { Username = "client", Password = "secret" },
        };

        var result = validator.TestValidate(request);

        result.Errors.Should().Contain(error => error.ErrorMessage.Contains("Authorization header", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WhenCreateSubscriptionUsesPrivateUrlInProduction_ShouldHaveValidationError()
    {
        var validator = new CreateSubscriptionRequestDtoValidator(
            new TestHostEnvironment(Environments.Production),
            Options.Create(new SecuritySettings { AllowPrivateNetworkTargetUrls = false }));
        var request = CreateSubscriptionRequest("http://127.0.0.1/webhook");

        var result = validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.TargetUrl);
    }

    [Fact]
    public void Validate_WhenUpdateSubscriptionUsesInvalidApiKeyHeader_ShouldHaveValidationError()
    {
        var validator = new UpdateSubscriptionRequestDtoValidator();
        var request = new UpdateSubscriptionRequestDto
        {
            Authentication = new AuthenticationDto
            {
                Type = "ApiKeyHeader",
                ApiKeyHeader = new ApiKeyHeaderDto { HeaderName = "Bad\r\nHeader", HeaderValue = "secret" },
            },
        };

        var result = validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Authentication);
    }

    private static CreateSubscriptionRequestDto CreateSubscriptionRequest(string targetUrl = "https://hooks.example.com/orders") => new()
    {
        EventType = "order.created",
        TargetUrl = targetUrl,
        TimeoutSeconds = 10,
        Headers = [],
        RetryPolicy = new RetryPolicyDto { MaxAttempts = 3, InitialDelaySeconds = 10, BackoffType = "Exponential" },
    };

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "HookBridge.Application.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
