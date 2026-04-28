using FluentValidation;
using HookBridge.Application.DTOs.ApiKeys;

namespace HookBridge.Application.Validation.ApiKeys;

public sealed class UpdateApiKeyRequestDtoValidator : AbstractValidator<UpdateApiKeyRequestDto>
{
    public UpdateApiKeyRequestDtoValidator()
    {
        RuleFor(x => x.AllowedIpAddresses)
            .Must(list => list is null || list.Count <= 50)
            .WithMessage("AllowedIpAddresses can contain at most 50 entries.");

        RuleForEach(x => x.AllowedIpAddresses)
            .Must(IpAllowlistValidationHelper.IsValidIpOrCidr)
            .WithMessage("AllowedIpAddresses must contain valid IP addresses or CIDR ranges.")
            .When(x => x.AllowedIpAddresses is not null);
    }
}
