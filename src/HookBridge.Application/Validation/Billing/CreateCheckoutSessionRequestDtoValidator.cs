using FluentValidation;
using HookBridge.Application.DTOs.Billing;
using HookBridge.Domain.Enums;

namespace HookBridge.Application.Validation.Billing;

public sealed class CreateCheckoutSessionRequestDtoValidator : AbstractValidator<CreateCheckoutSessionRequestDto>
{
    public CreateCheckoutSessionRequestDtoValidator()
    {
        RuleFor(x => x.Plan)
            .Must(plan => plan is BillingPlan.Starter or BillingPlan.Pro or BillingPlan.Enterprise)
            .WithMessage("Plan must be Starter, Pro, or Enterprise for checkout.");
    }
}
