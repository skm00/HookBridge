using FluentValidation;
using HookBridge.Application.DTOs.ApiKeys;

namespace HookBridge.Application.Validation.ApiKeys;

public sealed class CreateApiKeyRequestDtoValidator : AbstractValidator<CreateApiKeyRequestDto>
{
    public CreateApiKeyRequestDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}
