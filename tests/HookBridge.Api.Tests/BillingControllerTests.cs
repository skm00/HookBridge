using System.Text;
using HookBridge.Api.Controllers;
using HookBridge.Application.DTOs.Billing;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace HookBridge.Api.Tests;

public sealed class BillingControllerTests
{
    [Fact]
    public async Task HandleStripeWebhook_InvalidSignature_Returns400()
    {
        var controller = BuildController(new FakeBillingService(throwStripeException: true));
        controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        controller.Request.Headers["Stripe-Signature"] = "bad-signature";

        var result = await controller.HandleStripeWebhookAsync(CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    private static BillingController BuildController(IBillingService billingService)
        => new(billingService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

    private sealed class FakeBillingService(bool throwStripeException = false) : IBillingService
    {
        public Task<CheckoutSessionResponseDto> CreateCheckoutSessionAsync(string tenantId, CreateCheckoutSessionRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new CheckoutSessionResponseDto { SessionId = "cs_123", CheckoutUrl = "https://checkout.stripe.com" });

        public Task<BillingStatusResponseDto?> GetBillingStatusAsync(string tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<BillingStatusResponseDto?>(new BillingStatusResponseDto
            {
                TenantId = tenantId,
                Plan = BillingPlan.Free,
                MonthlyEventLimit = 1000,
                BillingStatus = "Free",
            });

        public Task HandleStripeWebhookAsync(string jsonPayload, string stripeSignature, CancellationToken cancellationToken = default)
        {
            if (throwStripeException)
            {
                throw new StripeException("invalid signature");
            }

            return Task.CompletedTask;
        }
    }
}
