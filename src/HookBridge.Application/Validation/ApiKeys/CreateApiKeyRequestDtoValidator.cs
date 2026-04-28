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

        RuleFor(x => x.AllowedIpAddresses)
            .Must(list => list is null || list.Count <= 50)
            .WithMessage("AllowedIpAddresses can contain at most 50 entries.");

        RuleForEach(x => x.AllowedIpAddresses)
            .Must(IpAllowlistValidationHelper.IsValidIpOrCidr)
            .WithMessage("AllowedIpAddresses must contain valid IP addresses or CIDR ranges.")
            .When(x => x.AllowedIpAddresses is not null);
    }
}
