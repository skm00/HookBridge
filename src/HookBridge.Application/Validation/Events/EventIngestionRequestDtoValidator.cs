using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using HookBridge.Application.DTOs.Events;
using HookBridge.Shared.Constants;

namespace HookBridge.Application.Validation.Events;

public sealed partial class EventIngestionRequestDtoValidator : AbstractValidator<EventIngestionRequestDto>
{
    public EventIngestionRequestDtoValidator()
    {
        RuleFor(x => x.EventType)
            .MaximumLength(ValidationLimits.MaxEventTypeLength)
            .When(x => !string.IsNullOrWhiteSpace(x.EventType))
            .MaximumLength(ValidationLimits.MaxEventTypeLength)
            .WithMessage($"EventType must be {ValidationLimits.MaxEventTypeLength} characters or fewer.")
            .Matches(EventTypeRegex())
            .When(x => !string.IsNullOrWhiteSpace(x.EventType))
            .WithMessage("EventType may contain only letters, numbers, dot (.), dash (-), and underscore (_).");

        RuleFor(x => x.EventId)
            .MaximumLength(ValidationLimits.MaxEventIdLength)
            .When(x => !string.IsNullOrWhiteSpace(x.EventId))
            .WithMessage($"EventId must be {ValidationLimits.MaxEventIdLength} characters or fewer.");

        RuleFor(x => x.Data)
            .NotNull()
            .WithMessage("Payload is required.")
            .Must(BeJsonObject)
            .WithMessage("Payload must be a JSON object.")
            .Must(BeWithinPayloadLimit)
            .WithMessage($"Data payload exceeds {ValidationLimits.MaxPayloadSizeBytes} bytes.");
    }

    private static bool BeJsonObject(object? data)
    {
        return data is JsonElement element
            ? element.ValueKind == JsonValueKind.Object
            : data is not null;
    }

    private static bool BeWithinPayloadLimit(object? data)
    {
        if (data is null)
        {
            return false;
        }

        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(data);
        return payloadBytes.Length <= ValidationLimits.MaxPayloadSizeBytes;
    }

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex EventTypeRegex();
}
