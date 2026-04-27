using FluentValidation;
using HookBridge.Application.DTOs.Subscriptions;

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

    public UpdateSubscriptionRequestDtoValidator()
    {
        RuleFor(x => x.EventType)
            .MaximumLength(150)
            .When(x => x.EventType is not null);

        RuleFor(x => x.TargetUrl)
            .Must(BeValidAbsoluteUrl)
            .When(x => x.TargetUrl is not null)
            .WithMessage("TargetUrl must be a valid absolute URL.");

        RuleFor(x => x.TimeoutSeconds)
            .Must(x => x is null || x is >= 1 and <= 120)
            .WithMessage("TimeoutSeconds must be between 1 and 120.");

        When(x => x.RetryPolicy is not null, () =>
        {
            RuleFor(x => x.RetryPolicy!.MaxAttempts)
                .InclusiveBetween(1, 10);

            RuleFor(x => x.RetryPolicy!.InitialDelaySeconds)
                .InclusiveBetween(1, 3600);

            RuleFor(x => x.RetryPolicy!.BackoffType)
                .Must(x => x is "Fixed" or "Exponential")
                .WithMessage("RetryPolicy.BackoffType must be Fixed or Exponential.");
        });

        RuleFor(x => x.Headers)
            .Must(HaveUniqueHeaderNames)
            .When(x => x.Headers is not null)
            .WithMessage("Headers cannot contain duplicate names (case-insensitive).")
            .DependentRules(() =>
            {
                RuleForEach(x => x.Headers!)
                    .ChildRules(header =>
                    {
                        header.RuleFor(x => x.Name).NotEmpty();
                        header.RuleFor(x => x.Value).NotEmpty();
                    });
            });

        RuleFor(x => x.Authentication)
            .Must(BeValidAuthentication)
            .WithMessage("Authentication type/configuration is invalid.");
    }

    private static bool BeValidAbsoluteUrl(string? url)
        => !string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out _);

    private static bool HaveUniqueHeaderNames(List<KeyValueDto>? headers)
        => headers is not null && headers
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .All(group => group.Count() == 1);

    private static bool BeValidAuthentication(AuthenticationDto? authentication)
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
                && !string.IsNullOrWhiteSpace(authentication.OAuth2.TokenUrl)
                && !string.IsNullOrWhiteSpace(authentication.OAuth2.ClientId)
                && !string.IsNullOrWhiteSpace(authentication.OAuth2.ClientSecret),
            "ApiKeyHeader" => authentication.ApiKeyHeader is not null
                && !string.IsNullOrWhiteSpace(authentication.ApiKeyHeader.HeaderName)
                && !string.IsNullOrWhiteSpace(authentication.ApiKeyHeader.HeaderValue),
            "HmacSignature" => authentication.HmacSignature is not null
                && !string.IsNullOrWhiteSpace(authentication.HmacSignature.Secret)
                && !string.IsNullOrWhiteSpace(authentication.HmacSignature.HeaderName)
                && !string.IsNullOrWhiteSpace(authentication.HmacSignature.Algorithm),
            _ => false,
        };
    }
}
