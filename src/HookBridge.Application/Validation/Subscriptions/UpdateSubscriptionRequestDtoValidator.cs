using FluentValidation;
using HookBridge.Application.Configuration;
using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Shared.Constants;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HookBridge.Application.Validation.Subscriptions;

public sealed class UpdateSubscriptionRequestDtoValidator : AbstractValidator<UpdateSubscriptionRequestDto>
{
    private static readonly string[] AllowedAuthenticationTypes =
    [
        "None",
        "Basic",
        "OAuth2ClientCredentials",
        "ApiKeyHeader",
        "HmacSignature",
    ];

    public UpdateSubscriptionRequestDtoValidator(
        IHostEnvironment? hostEnvironment = null,
        IOptions<SecuritySettings>? securitySettings = null)
    {
        var isProduction = hostEnvironment?.IsProduction() ?? false;
        var allowPrivateNetworkTargetUrls = securitySettings?.Value.AllowPrivateNetworkTargetUrls ?? true;

        RuleFor(x => x.EventType)
            .MaximumLength(ValidationLimits.MaxEventTypeLength)
            .When(x => x.EventType is not null);

        RuleFor(x => x.TargetUrl)
            .MaximumLength(ValidationLimits.MaxTargetUrlLength)
            .When(x => x.TargetUrl is not null)
            .WithMessage($"TargetUrl must be {ValidationLimits.MaxTargetUrlLength} characters or fewer.")
            .Must(SecurityValidationHelpers.BeValidHttpUrl)
            .When(x => x.TargetUrl is not null)
            .WithMessage("TargetUrl must be an absolute HTTP or HTTPS URL.")
            .Must(url => url is null
                || !isProduction
                || allowPrivateNetworkTargetUrls
                || !SecurityValidationHelpers.IsPrivateOrLocalNetworkTarget(url))
            .WithMessage("TargetUrl cannot point to localhost or private network addresses in Production.");

        RuleFor(x => x.TimeoutSeconds)
            .Must(x => x is null || x is >= 1 and <= 120)
            .WithMessage("TimeoutSeconds must be between 1 and 120.");

        When(x => x.RetryPolicy is not null, () =>
        {
            RuleFor(x => x.RetryPolicy!.MaxAttempts).InclusiveBetween(1, 10);
            RuleFor(x => x.RetryPolicy!.InitialDelaySeconds).InclusiveBetween(1, 3600);
            RuleFor(x => x.RetryPolicy!.BackoffType)
                .Must(x => x is "Fixed" or "Exponential")
                .WithMessage("RetryPolicy.BackoffType must be Fixed or Exponential.");
        });

        RuleFor(x => x.Headers)
            .Must(headers => headers is null || headers.Count <= ValidationLimits.MaxCustomHeaders)
            .WithMessage($"A maximum of {ValidationLimits.MaxCustomHeaders} custom headers is allowed.")
            .Must(SecurityValidationHelpers.HaveUniqueHeaderNames)
            .When(x => x.Headers is not null)
            .WithMessage("Headers cannot contain duplicate names (case-insensitive).")
            .DependentRules(() =>
            {
                RuleForEach(x => x.Headers!)
                    .ChildRules(header =>
                    {
                        header.RuleFor(x => x.Name)
                            .NotEmpty()
                            .WithMessage("Header name is required.")
                            .MaximumLength(ValidationLimits.MaxHeaderNameLength)
                            .WithMessage($"Header name must be {ValidationLimits.MaxHeaderNameLength} characters or fewer.")
                            .Must(name => !SecurityValidationHelpers.ContainsCrLf(name))
                            .WithMessage("Header name must not contain CR or LF characters.")
                            .Must(name => !SecurityValidationHelpers.IsRestrictedOutboundHeader(name))
                            .WithMessage("Header name is restricted and cannot be set explicitly.");

                        header.RuleFor(x => x.Value)
                            .NotEmpty()
                            .WithMessage("Header value is required.")
                            .MaximumLength(ValidationLimits.MaxHeaderValueLength)
                            .WithMessage($"Header value must be {ValidationLimits.MaxHeaderValueLength} characters or fewer.")
                            .Must(value => !SecurityValidationHelpers.ContainsCrLf(value))
                            .WithMessage("Header value must not contain CR or LF characters.");
                    });
            });

        RuleFor(x => x)
            .Must(request => request.Headers is null
                || !SecurityValidationHelpers.AuthenticationSetsAuthorizationHeader(request.Authentication)
                || request.Headers.All(header => !header.Name.Equals("Authorization", StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Authorization header cannot be set when authentication configuration already sets Authorization.");

        RuleFor(x => x.Authentication)
            .Must(auth => BeValidAuthentication(auth, isProduction))
            .WithMessage("Authentication type/configuration is invalid.");
    }

    private static bool BeValidAuthentication(AuthenticationDto? authentication, bool isProduction)
    {
        if (authentication is null)
        {
            return true;
        }

        if (!AllowedAuthenticationTypes.Contains(authentication.Type))
        {
            return false;
        }

        return authentication.Type switch
        {
            "None" => true,
            "Basic" => authentication.Basic is not null
                && !string.IsNullOrWhiteSpace(authentication.Basic.Username)
                && !string.IsNullOrWhiteSpace(authentication.Basic.Password),
            "OAuth2ClientCredentials" => authentication.OAuth2 is not null
                && !string.IsNullOrWhiteSpace(authentication.OAuth2.ClientId)
                && !string.IsNullOrWhiteSpace(authentication.OAuth2.ClientSecret)
                && SecurityValidationHelpers.BeValidHttpUrl(authentication.OAuth2.TokenUrl)
                && (!isProduction || Uri.TryCreate(authentication.OAuth2.TokenUrl, UriKind.Absolute, out var uri)
                    && uri.Scheme == Uri.UriSchemeHttps),
            "ApiKeyHeader" => authentication.ApiKeyHeader is not null
                && SecurityValidationHelpers.IsSafeHeaderName(authentication.ApiKeyHeader.HeaderName)
                && SecurityValidationHelpers.IsSafeHeaderValue(authentication.ApiKeyHeader.HeaderValue),
            "HmacSignature" => authentication.HmacSignature is not null
                && !string.IsNullOrWhiteSpace(authentication.HmacSignature.Secret)
                && SecurityValidationHelpers.IsSafeHeaderName(authentication.HmacSignature.HeaderName)
                && authentication.HmacSignature.Algorithm.Equals("HMACSHA256", StringComparison.Ordinal),
            _ => false,
        };
    }
}
