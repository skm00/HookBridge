using HookBridge.Application.DTOs.ApiKeys;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/admin/tenants/{tenantId}/api-keys")]
public sealed class ApiKeysController(IApiKeyService apiKeyService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(CreateApiKeyResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateApiKeyResponseDto>> CreateAsync(
        string tenantId,
        [FromBody] CreateApiKeyRequestDto request,
        CancellationToken cancellationToken)
    {
        var created = await apiKeyService.CreateAsync(tenantId, request, cancellationToken);
        return CreatedAtAction(nameof(GetByTenantAsync), new { tenantId }, created);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApiKeyResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ApiKeyResponseDto>>> GetByTenantAsync(string tenantId, CancellationToken cancellationToken)
    {
        var keys = await apiKeyService.GetByTenantAsync(tenantId, cancellationToken);
        return Ok(keys);
    }

    [HttpDelete("{keyId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeAsync(string tenantId, string keyId, CancellationToken cancellationToken)
    {
        var revoked = await apiKeyService.RevokeAsync(tenantId, keyId, cancellationToken);
        if (!revoked)
        {
            return NotFound();
        }

        return NoContent();
    }
}
