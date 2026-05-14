using System.Text.Json;
using FluentValidation;
using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Validation;

public sealed class WebhookTransformationRecommendationRequestDtoValidator : AbstractValidator<WebhookTransformationRecommendationRequestDto>
{
    public WebhookTransformationRecommendationRequestDtoValidator()
    {
        RuleFor(x => x.EventId).NotEmpty().WithMessage("EventId is required.");
        RuleFor(x => x.SourcePayload).NotNull().WithMessage("SourcePayload is required.");
        RuleFor(x => x.ReceivedAtUtc).Must(BeUtc).WithMessage("ReceivedAtUtc must be UTC.");
        RuleFor(x => x.SourcePayload).Must(BeValidJson).When(x => x.SourcePayload is not null).WithMessage("SourcePayload must be valid JSON.");
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;

    private static bool BeValidJson(object? payload)
    {
        try
        {
            var json = payload switch { null => string.Empty, string s => s, JsonElement e => e.GetRawText(), _ => JsonSerializer.Serialize(payload) };
            if (string.IsNullOrWhiteSpace(json)) return false;
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException) { return false; }
    }
}
