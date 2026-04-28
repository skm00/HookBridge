using FluentValidation;
using HookBridge.Application.DTOs.Tenants;

namespace HookBridge.Application.Validation.Tenants;

public sealed class CreateTenantRequestDtoValidator : AbstractValidator<CreateTenantRequestDto>
{
    public CreateTenantRequestDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[a-z0-9-]+$")
            .WithMessage("Slug must contain only lowercase letters, numbers, and hyphen.");

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
