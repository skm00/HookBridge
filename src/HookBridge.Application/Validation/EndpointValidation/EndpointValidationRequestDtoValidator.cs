using FluentValidation;
using HookBridge.Application.Configuration;
using HookBridge.Application.DTOs.EndpointValidation;
using HookBridge.Application.Validation.Subscriptions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HookBridge.Application.Validation.EndpointValidation;

public sealed class EndpointValidationRequestDtoValidator : AbstractValidator<EndpointValidationRequestDto>
{
    public EndpointValidationRequestDtoValidator(IOptions<SecuritySettings> securityOptions, IHostEnvironment environment)
    {
        RuleFor(x => x.TargetUrl)
            .NotEmpty()
            .Must(SecurityValidationHelpers.BeValidHttpUrl).WithMessage("TargetUrl must be an absolute HTTP/HTTPS URL.");

        RuleFor(x => x.TimeoutSeconds)
            .InclusiveBetween(1, 30);

        RuleFor(x => x.TargetUrl)
            .Must(url => securityOptions.Value.AllowPrivateNetworkTargetUrls || !SecurityValidationHelpers.IsPrivateOrLocalNetworkTarget(url))
            .WithMessage("TargetUrl cannot point to localhost or a private network target in this environment.")
            .When(_ => environment.IsProduction());
    }
}
