using FluentValidation;
using HookBridge.Application.DTOs.Billing;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Configuration;
using HookBridge.Domain.Constants;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using StripeSubscription = Stripe.Subscription;

namespace HookBridge.Infrastructure.Services.Billing;

public sealed class BillingService(
    IMongoRepository<Tenant> tenantRepository,
    IAuditLogService auditLogService,
    IValidator<CreateCheckoutSessionRequestDto> checkoutValidator,
    IOptions<StripeSettings> stripeOptions,
    IStripeGateway stripeGateway,
    INotificationService notificationService,
    ILogger<BillingService> logger) : IBillingService
{
    private readonly StripeSettings stripeSettings = stripeOptions.Value;

    public async Task<CheckoutSessionResponseDto> CreateCheckoutSessionAsync(
        string tenantId,
        CreateCheckoutSessionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await checkoutValidator.ValidateAndThrowAsync(request, cancellationToken);

        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant '{tenantId}' was not found.");

        StripeConfiguration.ApiKey = stripeSettings.SecretKey;

        var priceId = MapPriceId(request.Plan);

        if (string.IsNullOrWhiteSpace(tenant.StripeCustomerId))
        {
            var customer = await stripeGateway.CreateCustomerAsync(new CustomerCreateOptions
            {
                Name = tenant.Name,
                Email = tenant.ContactEmail,
                Metadata = new Dictionary<string, string>
                {
                    ["tenantId"] = tenant.Id,
                },
            }, cancellationToken);

            tenant.StripeCustomerId = customer.Id;
            await tenantRepository.UpdateAsync(tenant, cancellationToken);
        }

        var session = await stripeGateway.CreateCheckoutSessionAsync(new SessionCreateOptions
        {
            Customer = tenant.StripeCustomerId,
            Mode = "subscription",
            SuccessUrl = stripeSettings.SuccessUrl,
            CancelUrl = stripeSettings.CancelUrl,
            Metadata = new Dictionary<string, string>
            {
                ["tenantId"] = tenant.Id,
                ["plan"] = request.Plan.ToString(),
            },
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Price = priceId,
                    Quantity = 1,
                },
            },
        }, cancellationToken);
        await TryAuditAsync(
            new AuditLog
            {
                TenantId = tenant.Id,
                Action = "BillingCheckoutSessionCreated",
                ResourceType = "Billing",
                ResourceId = session.Id,
                Description = $"Stripe checkout session created for plan '{request.Plan}'.",
                Metadata = new Dictionary<string, object?>
                {
                    ["plan"] = request.Plan.ToString(),
                    ["stripeCustomerId"] = tenant.StripeCustomerId,
                    ["sessionId"] = session.Id,
                },
            },
            cancellationToken);

        return new CheckoutSessionResponseDto
        {
            SessionId = session.Id,
            CheckoutUrl = session.Url ?? string.Empty,
        };
    }

    public async Task<BillingStatusResponseDto?> GetBillingStatusAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        return new BillingStatusResponseDto
        {
            TenantId = tenant.Id,
            Plan = tenant.Plan,
            MonthlyEventLimit = tenant.MonthlyEventLimit,
            BillingStatus = tenant.BillingStatus,
            StripeCustomerId = tenant.StripeCustomerId,
            StripeSubscriptionId = tenant.StripeSubscriptionId,
            CurrentPeriodStart = tenant.CurrentPeriodStart,
            CurrentPeriodEnd = tenant.CurrentPeriodEnd,
        };
    }

    public async Task HandleStripeWebhookAsync(
        string jsonPayload,
        string stripeSignature,
        CancellationToken cancellationToken = default)
    {
        StripeConfiguration.ApiKey = stripeSettings.SecretKey;

        var stripeEvent = stripeGateway.ConstructWebhookEvent(jsonPayload, stripeSignature, stripeSettings.WebhookSecret);

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                await HandleCheckoutSessionCompletedAsync(stripeEvent, cancellationToken);
                break;
            case "customer.subscription.updated":
                await HandleSubscriptionUpdatedAsync(stripeEvent, cancellationToken);
                break;
            case "customer.subscription.deleted":
                await HandleSubscriptionDeletedAsync(stripeEvent, cancellationToken);
                break;
            case "invoice.payment_failed":
                await HandleInvoicePaymentFailedAsync(stripeEvent, cancellationToken);
                break;
            default:
                logger.LogDebug("Unhandled Stripe event type: {EventType}", stripeEvent.Type);
                break;
        }
    }

    private async Task HandleCheckoutSessionCompletedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not Session session)
        {
            return;
        }

        var tenant = await FindTenantAsync(session.Metadata?.GetValueOrDefault("tenantId"), session.CustomerId, cancellationToken);
        if (tenant is null)
        {
            return;
        }

        tenant.StripeCustomerId = session.CustomerId ?? tenant.StripeCustomerId;
        tenant.StripeSubscriptionId = session.SubscriptionId ?? tenant.StripeSubscriptionId;

        var plan = ParsePlan(session.Metadata?.GetValueOrDefault("plan")) ?? tenant.Plan;
        ApplyPlan(tenant, plan, "Active");

        await tenantRepository.UpdateAsync(tenant, cancellationToken);
        await TryAuditAsync(BuildWebhookAuditLog(tenant, "BillingPlanStatusUpdatedFromStripeWebhook", stripeEvent.Type), cancellationToken);
    }

    private async Task HandleSubscriptionUpdatedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not StripeSubscription subscription)
        {
            return;
        }

        var tenant = await FindTenantAsync(null, subscription.CustomerId, cancellationToken);
        if (tenant is null)
        {
            return;
        }

        tenant.StripeCustomerId = subscription.CustomerId ?? tenant.StripeCustomerId;
        tenant.StripeSubscriptionId = subscription.Id;

        var plan = MapPlanFromPriceId(subscription.Items.Data.FirstOrDefault()?.Price?.Id) ?? tenant.Plan;
        ApplyPlan(tenant, plan, MapSubscriptionStatus(subscription.Status));
        SetPeriodDates(tenant, subscription);

        await tenantRepository.UpdateAsync(tenant, cancellationToken);
        await TryAuditAsync(BuildWebhookAuditLog(tenant, "BillingPlanStatusUpdatedFromStripeWebhook", stripeEvent.Type), cancellationToken);
    }

    private async Task HandleSubscriptionDeletedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not StripeSubscription subscription)
        {
            return;
        }

        var tenant = await FindTenantAsync(null, subscription.CustomerId, cancellationToken);
        if (tenant is null)
        {
            return;
        }

        tenant.StripeSubscriptionId = null;
        ApplyPlan(tenant, BillingPlan.Free, "Canceled");
        tenant.CurrentPeriodStart = null;
        tenant.CurrentPeriodEnd = null;

        await tenantRepository.UpdateAsync(tenant, cancellationToken);
        await TryAuditAsync(BuildWebhookAuditLog(tenant, "BillingPlanStatusUpdatedFromStripeWebhook", stripeEvent.Type), cancellationToken);
    }

    private async Task HandleInvoicePaymentFailedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not Invoice invoice)
        {
            return;
        }

        var tenant = await FindTenantAsync(null, invoice.CustomerId, cancellationToken);
        if (tenant is null)
        {
            return;
        }

        tenant.BillingStatus = "PaymentFailed";
        await tenantRepository.UpdateAsync(tenant, cancellationToken);

        await notificationService.CreateAsync(new Notification
        {
            TenantId = tenant.Id,
            Type = NotificationTypes.BillingPaymentFailed,
            Severity = NotificationSeverities.Critical,
            Title = "Billing payment failed",
            Message = "Stripe reported a failed payment. Please update your billing method.",
            IsRead = false,
        }, cancellationToken);

        await TryAuditAsync(BuildWebhookAuditLog(tenant, "BillingPlanStatusUpdatedFromStripeWebhook", stripeEvent.Type), cancellationToken);
    }

    private async Task<Tenant?> FindTenantAsync(string? tenantId, string? stripeCustomerId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            var byId = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
            if (byId is not null)
            {
                return byId;
            }
        }

        if (string.IsNullOrWhiteSpace(stripeCustomerId))
        {
            return null;
        }

        return await tenantRepository.FirstOrDefaultAsync(x => x.StripeCustomerId == stripeCustomerId, cancellationToken);
    }

    private void ApplyPlan(Tenant tenant, BillingPlan plan, string billingStatus)
    {
        tenant.Plan = plan;
        tenant.BillingStatus = billingStatus;
        tenant.MonthlyEventLimit = BillingPlanLimits.GetMonthlyLimit(plan);
    }

    private static BillingPlan? ParsePlan(string? plan)
    {
        if (Enum.TryParse<BillingPlan>(plan, true, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private BillingPlan? MapPlanFromPriceId(string? priceId)
    {
        if (string.Equals(priceId, stripeSettings.StarterPriceId, StringComparison.Ordinal))
        {
            return BillingPlan.Starter;
        }

        if (string.Equals(priceId, stripeSettings.ProPriceId, StringComparison.Ordinal))
        {
            return BillingPlan.Pro;
        }

        if (string.Equals(priceId, stripeSettings.EnterprisePriceId, StringComparison.Ordinal))
        {
            return BillingPlan.Enterprise;
        }

        return null;
    }

    private string MapPriceId(BillingPlan plan) => plan switch
    {
        BillingPlan.Starter => stripeSettings.StarterPriceId,
        BillingPlan.Pro => stripeSettings.ProPriceId,
        BillingPlan.Enterprise => stripeSettings.EnterprisePriceId,
        _ => throw new ValidationException("Free plan cannot create a Stripe checkout session."),
    };

    private static string MapSubscriptionStatus(string? status) => status switch
    {
        "active" => "Active",
        "past_due" => "PastDue",
        "unpaid" => "Unpaid",
        "trialing" => "Trialing",
        "canceled" => "Canceled",
        _ => "Active",
    };

    private static void SetPeriodDates(Tenant tenant, StripeSubscription subscription)
    {
        tenant.CurrentPeriodStart = ReadStripeDateTime(subscription, "CurrentPeriodStart")
            ?? ReadStripeDateTime(subscription.Items.Data.FirstOrDefault(), "CurrentPeriodStart");
        tenant.CurrentPeriodEnd = ReadStripeDateTime(subscription, "CurrentPeriodEnd")
            ?? ReadStripeDateTime(subscription.Items.Data.FirstOrDefault(), "CurrentPeriodEnd");
    }

    private static DateTime? ReadStripeDateTime(object? instance, string propertyName)
    {
        if (instance is null)
        {
            return null;
        }

        var value = instance.GetType().GetProperty(propertyName)?.GetValue(instance);
        return value switch
        {
            null => null,
            DateTime dateTime => dateTime,
            DateTimeOffset dateTimeOffset => dateTimeOffset.UtcDateTime,
            long unixSeconds => DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime,
            int unixSeconds => DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime,
            _ => null,
        };
    }

    private static AuditLog BuildWebhookAuditLog(Tenant tenant, string action, string eventType)
        => new()
        {
            TenantId = tenant.Id,
            Action = action,
            ResourceType = nameof(Tenant),
            ResourceId = tenant.Id,
            Description = $"Tenant billing updated from Stripe webhook '{eventType}'.",
            Metadata = new Dictionary<string, object?>
            {
                ["eventType"] = eventType,
                ["plan"] = tenant.Plan.ToString(),
                ["billingStatus"] = tenant.BillingStatus,
                ["stripeCustomerId"] = tenant.StripeCustomerId,
                ["stripeSubscriptionId"] = tenant.StripeSubscriptionId,
            },
        };

    private async Task TryAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        try
        {
            await auditLogService.LogAsync(auditLog, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Audit logging failed for action {Action}.", auditLog.Action);
        }
    }
}
