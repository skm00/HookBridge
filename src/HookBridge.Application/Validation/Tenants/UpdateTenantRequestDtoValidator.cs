using FluentValidation;
using HookBridge.Application.DTOs.Tenants;

namespace HookBridge.Application.Validation.Tenants;

public sealed class UpdateTenantRequestDtoValidator : AbstractValidator<UpdateTenantRequestDto>
{
    public UpdateTenantRequestDtoValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(150)
            .When(x => !string.IsNullOrWhiteSpace(x.Name));

        RuleFor(x => x.ContactEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
    }
}
