using Asp.Versioning;
using FluentValidation;
using HookBridge.Api.Authorization;
using HookBridge.Api.RateLimiting;
using HookBridge.Api.Security;
using HookBridge.Application.DTOs.EndpointValidation;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace HookBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[EnableRateLimiting(RateLimitingPolicyNames.AdminApiPolicy)]
[Route("api/v{version:apiVersion}/admin/endpoint-validation")]
public sealed class EndpointValidationController(
    IEndpointValidationService endpointValidationService,
    IValidator<EndpointValidationRequestDto> validator,
    ICurrentUserContext currentUserContext,
    TenantAccessValidator tenantAccessValidator) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<EndpointValidationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<EndpointValidationResponseDto>>> ValidateAsync(
        [FromBody] EndpointValidationRequestDto request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUserContext.TenantId ?? string.Empty;
        tenantAccessValidator.EnsureTenantAccess(tenantId);

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var modelState = new ModelStateDictionary();
            foreach (var (key, errors) in validationResult.ToDictionary())
            {
                foreach (var error in errors)
                {
                    modelState.AddModelError(key, error);
                }
            }

            return ValidationProblem(modelState);
        }

        var response = await endpointValidationService.ValidateAsync(request, cancellationToken);
        return OkResponse(response);
    }
}
