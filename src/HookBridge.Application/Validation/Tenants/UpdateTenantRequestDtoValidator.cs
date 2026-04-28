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

        RuleFor(x => x.NotificationEmails)
            .Must(emails => emails.Count <= 10)
            .WithMessage("A maximum of 10 notification emails is allowed.");

        RuleForEach(x => x.NotificationEmails)
            .EmailAddress();
    }
}
