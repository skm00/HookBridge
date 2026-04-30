using HookBridge.Application.DTOs.EndpointValidation;

namespace HookBridge.Application.Interfaces.Services;

public interface IEndpointValidationService
{
    Task<EndpointValidationResponseDto> ValidateAsync(EndpointValidationRequestDto request, CancellationToken cancellationToken = default);
}
