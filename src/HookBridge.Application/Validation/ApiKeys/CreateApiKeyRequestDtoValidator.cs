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

        RuleFor(x => x.SignatureSecret)
            .NotEmpty()
            .When(x => x.EnableSignatureValidation);

        RuleFor(x => x.SignatureHeaderName)
            .NotEmpty()
            .MaximumLength(100)
            .Must(value => value is not null && !value.Contains('\r') && !value.Contains('\n'))
            .WithMessage("SignatureHeaderName contains invalid characters.");
    }
}
