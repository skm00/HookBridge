using FluentValidation;
using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Validation;

public sealed class WebhookFailureAnalysisRequestDtoValidator : AbstractValidator<WebhookFailureAnalysisRequestDto>
{
    public WebhookFailureAnalysisRequestDtoValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty()
            .WithMessage("EventId is required.");

        RuleFor(x => x.EventType)
            .NotEmpty()
            .WithMessage("EventType is required.");

        RuleFor(x => x.StatusCode)
            .InclusiveBetween(100, 599)
            .When(x => x.StatusCode.HasValue)
            .WithMessage("StatusCode must be between 100 and 599.");

        RuleFor(x => x.RetryCount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("RetryCount must be 0 or greater.");

        RuleFor(x => x.MaxRetryCount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("MaxRetryCount must be 0 or greater.");

        RuleFor(x => x.FailedAtUtc)
            .Must(BeUtc)
            .WithMessage("FailedAtUtc must be UTC.");

        RuleFor(x => x.TargetUrl)
            .Must(BeAbsoluteHttpUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.TargetUrl))
            .WithMessage("TargetUrl must be a valid absolute HTTP or HTTPS URL.");
    }

    private static bool BeUtc(DateTime failedAtUtc)
        => failedAtUtc.Kind == DateTimeKind.Utc;

    private static bool BeAbsoluteHttpUrl(string? targetUrl)
        => Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri) &&
           (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
