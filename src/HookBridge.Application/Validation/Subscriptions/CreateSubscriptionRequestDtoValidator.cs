using FluentValidation;
using HookBridge.Application.DTOs.Subscriptions;

namespace HookBridge.Application.Validation.Subscriptions;

public sealed class CreateSubscriptionRequestDtoValidator : AbstractValidator<CreateSubscriptionRequestDto>
{
    private static readonly string[] AllowedAuthenticationTypes =
    [
        "None",
        "Basic",
        "OAuth2ClientCredentials",
        "ApiKeyHeader",
        "HmacSignature",
    ];

    public CreateSubscriptionRequestDtoValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty();

        RuleFor(x => x.EventType)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(x => x.TargetUrl)
            .NotEmpty()
            .Must(BeValidAbsoluteUrl)
            .WithMessage("TargetUrl must be a valid absolute URL.");

        RuleFor(x => x.TimeoutSeconds)
            .Must(x => x is null || x is >= 1 and <= 120)
            .WithMessage("TimeoutSeconds must be between 1 and 120.");

        RuleFor(x => x.RetryPolicy)
            .NotNull();

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
            .NotNull()
            .Must(HaveUniqueHeaderNames)
            .WithMessage("Headers cannot contain duplicate names (case-insensitive).");

        RuleFor(x => x.Authentication)
            .Must(BeValidAuthentication)
            .WithMessage("Authentication type/configuration is invalid.");
    }

    private static bool BeValidAbsoluteUrl(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out _);

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
            "Basic" => authentication.Basic is not null,
            "OAuth2ClientCredentials" => authentication.OAuth2 is not null,
            "ApiKeyHeader" => authentication.ApiKeyHeader is not null,
            "HmacSignature" => authentication.HmacSignature is not null,
            _ => false,
        };
    }
}
