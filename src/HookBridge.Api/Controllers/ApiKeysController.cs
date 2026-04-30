using Asp.Versioning;
using HookBridge.Application.DTOs.ApiKeys;
using HookBridge.Api.Authorization;
using HookBridge.Api.RateLimiting;
using HookBridge.Api.Security;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HookBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[EnableRateLimiting(RateLimitingPolicyNames.AdminApiPolicy)]
[Route("api/v{version:apiVersion}/admin/tenants/{tenantId}/api-keys")]
public sealed class ApiKeysController(
    IApiKeyService apiKeyService,
    TenantAccessValidator tenantAccessValidator) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOrOwner)]
    [ProducesResponseType(typeof(ApiResponse<CreateApiKeyResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CreateApiKeyResponseDto>>> CreateAsync(
        string tenantId,
        [FromBody] CreateApiKeyRequestDto request,
        CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(tenantId);
        var created = await apiKeyService.CreateAsync(tenantId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponseFactory.Success(created, traceId: TraceId));
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ApiKeyResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ApiKeyResponseDto>>>> GetByTenantAsync(string tenantId, CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(tenantId);
        var keys = await apiKeyService.GetByTenantAsync(tenantId, cancellationToken);
        return OkResponse(keys);
    }

    [HttpPut("{keyId}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrOwner)]
    [ProducesResponseType(typeof(ApiResponse<ApiKeyResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ApiKeyResponseDto>>> UpdateAsync(
        string tenantId,
        string keyId,
        [FromBody] UpdateApiKeyRequestDto request,
        CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(tenantId);
        var updated = await apiKeyService.UpdateAsync(tenantId, keyId, request, cancellationToken);
        if (updated is null)
        {
            return ErrorResponse<ApiKeyResponseDto>(StatusCodes.Status404NotFound, "Not found.");
        }

        return OkResponse(updated);
    }

    [HttpDelete("{keyId}")]
    [Authorize(Policy = AuthorizationPolicies.OwnerOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeAsync(string tenantId, string keyId, CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(tenantId);
        var revoked = await apiKeyService.RevokeAsync(tenantId, keyId, cancellationToken);
        if (!revoked)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Not found.");
        }

        return NoContent();
    }
}
