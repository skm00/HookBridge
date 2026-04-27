using FluentValidation;
using HookBridge.Application.DTOs.Auth;

namespace HookBridge.Application.Validation.Auth;

public sealed class RegisterAdminRequestDtoValidator : AbstractValidator<RegisterAdminRequestDto>
{
    public RegisterAdminRequestDtoValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty();

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8);

        RuleFor(x => x.FullName)
            .NotEmpty();
    }
}
