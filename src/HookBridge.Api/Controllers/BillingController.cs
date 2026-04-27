using HookBridge.Application.DTOs.Billing;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace HookBridge.Api.Controllers;

[ApiController]
public sealed class BillingController(IBillingService billingService) : ControllerBase
{
    [HttpPost("api/v1/admin/tenants/{tenantId}/billing/checkout")]
    [ProducesResponseType(typeof(CheckoutSessionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CheckoutSessionResponseDto>> CreateCheckoutAsync(
        string tenantId,
        [FromBody] CreateCheckoutSessionRequestDto request,
        CancellationToken cancellationToken)
    {
        var checkout = await billingService.CreateCheckoutSessionAsync(tenantId, request, cancellationToken);
        return Ok(checkout);
    }

    [HttpGet("api/v1/admin/tenants/{tenantId}/billing/status")]
    [ProducesResponseType(typeof(BillingStatusResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BillingStatusResponseDto>> GetStatusAsync(string tenantId, CancellationToken cancellationToken)
    {
        var status = await billingService.GetBillingStatusAsync(tenantId, cancellationToken);
        if (status is null)
        {
            return NotFound();
        }

        return Ok(status);
    }

    [HttpPost("api/v1/billing/stripe/webhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> HandleStripeWebhookAsync(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var jsonPayload = await reader.ReadToEndAsync(cancellationToken);

        if (!Request.Headers.TryGetValue("Stripe-Signature", out var stripeSignature) || string.IsNullOrWhiteSpace(stripeSignature))
        {
            return BadRequest(new { message = "Missing Stripe signature." });
        }

        try
        {
            await billingService.HandleStripeWebhookAsync(jsonPayload, stripeSignature.ToString(), cancellationToken);
            return Ok(new { received = true });
        }
        catch (StripeException)
        {
            return BadRequest(new { message = "Invalid Stripe webhook signature." });
        }
        catch (Exception)
        {
            return Ok(new { received = true });
        }
    }
}
