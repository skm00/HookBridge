using FluentValidation;
using HookBridge.Application.DTOs.Auth;

namespace HookBridge.Application.Validation.Auth;

public sealed class LoginRequestDtoValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty();

        RuleFor(x => x.Password)
            .NotEmpty();
    }
}
