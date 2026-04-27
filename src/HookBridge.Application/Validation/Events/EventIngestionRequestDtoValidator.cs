using FluentValidation;
using HookBridge.Application.DTOs.Events;

namespace HookBridge.Application.Validation.Events;

public sealed class EventIngestionRequestDtoValidator : AbstractValidator<EventIngestionRequestDto>
{
    public EventIngestionRequestDtoValidator()
    {
        RuleFor(x => x.EventType)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(x => x.EventId)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(x => x.Data)
            .NotNull();
    }
}
