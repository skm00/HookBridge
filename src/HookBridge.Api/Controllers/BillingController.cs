using Asp.Versioning;
using HookBridge.Application.DTOs.Billing;
using HookBridge.Api.Authorization;
using HookBridge.Api.Features;
using HookBridge.Api.RateLimiting;
using HookBridge.Api.Security;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Stripe;

namespace HookBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[RequireFeature("EnableBilling")]
public sealed class BillingController(
    IBillingService billingService,
    TenantAccessValidator tenantAccessValidator) : ApiControllerBase
{
    [HttpPost("api/v{version:apiVersion}/admin/tenants/{tenantId}/billing/checkout")]
    [EnableRateLimiting(RateLimitingPolicyNames.AdminApiPolicy)]
    [Authorize(Policy = AuthorizationPolicies.OwnerOnly)]
    [ProducesResponseType(typeof(ApiResponse<CheckoutSessionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<CheckoutSessionResponseDto>>> CreateCheckoutAsync(
        string tenantId,
        [FromBody] CreateCheckoutSessionRequestDto request,
        CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(tenantId);
        var checkout = await billingService.CreateCheckoutSessionAsync(tenantId, request, cancellationToken);
        return OkResponse(checkout);
    }

    [HttpGet("api/v{version:apiVersion}/admin/tenants/{tenantId}/billing/status")]
    [EnableRateLimiting(RateLimitingPolicyNames.AdminApiPolicy)]
    [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<BillingStatusResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BillingStatusResponseDto>>> GetStatusAsync(string tenantId, CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(tenantId);
        var status = await billingService.GetBillingStatusAsync(tenantId, cancellationToken);
        if (status is null)
        {
            return ErrorResponse<BillingStatusResponseDto>(StatusCodes.Status404NotFound, "Not found.");
        }

        return OkResponse(status);
    }

    [AllowAnonymous]
    [HttpPost("api/v{version:apiVersion}/billing/stripe/webhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> HandleStripeWebhookAsync(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var jsonPayload = await reader.ReadToEndAsync(cancellationToken);

        if (!Request.Headers.TryGetValue("Stripe-Signature", out var stripeSignature) || string.IsNullOrWhiteSpace(stripeSignature))
        {
            return ErrorResponse<object>(StatusCodes.Status400BadRequest, "Missing Stripe signature.");
        }

        try
        {
            await billingService.HandleStripeWebhookAsync(jsonPayload, stripeSignature.ToString(), cancellationToken);
            return OkResponse((object)new { received = true });
        }
        catch (StripeException)
        {
            return ErrorResponse<object>(StatusCodes.Status400BadRequest, "Invalid Stripe webhook signature.");
        }
        catch (Exception)
        {
            return OkResponse((object)new { received = true });
        }
    }
}
