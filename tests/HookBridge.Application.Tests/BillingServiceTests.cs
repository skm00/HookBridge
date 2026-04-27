using FluentValidation;
using HookBridge.Application.DTOs.Billing;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Validation.Billing;
using HookBridge.Domain.Configuration;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.Configuration;
using HookBridge.Infrastructure.Services.Billing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using Xunit;
using StripeSubscription = Stripe.Subscription;

namespace HookBridge.Application.Tests;

public sealed class BillingServiceTests
{
    [Fact]
    public async Task CreateCheckoutSession_FailsForFreePlan()
    {
        var service = CreateService(new InMemoryTenantRepository(new Tenant { Id = "tenant-1", Name = "Tenant 1" }), new FakeStripeGateway());

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateCheckoutSessionAsync("tenant-1", new CreateCheckoutSessionRequestDto
        {
            Plan = BillingPlan.Free,
        }));
    }

    [Fact]
    public async Task CreateCheckoutSession_MapsStarterPlanPriceId()
    {
        var stripe = new FakeStripeGateway();
        var service = CreateService(new InMemoryTenantRepository(new Tenant { Id = "tenant-1", Name = "Tenant 1" }), stripe);

        await service.CreateCheckoutSessionAsync("tenant-1", new CreateCheckoutSessionRequestDto { Plan = BillingPlan.Starter });

        Assert.Equal("price_starter", stripe.LastSessionOptions?.LineItems?.Single().Price);
    }

    [Fact]
    public async Task CreateCheckoutSession_MapsProPlanPriceId()
    {
        var stripe = new FakeStripeGateway();
        var service = CreateService(new InMemoryTenantRepository(new Tenant { Id = "tenant-1", Name = "Tenant 1" }), stripe);

        await service.CreateCheckoutSessionAsync("tenant-1", new CreateCheckoutSessionRequestDto { Plan = BillingPlan.Pro });

        Assert.Equal("price_pro", stripe.LastSessionOptions?.LineItems?.Single().Price);
    }

    [Fact]
    public async Task GetBillingStatus_ReturnsPlanAndLimit()
    {
        var service = CreateService(
            new InMemoryTenantRepository(new Tenant { Id = "tenant-1", Plan = BillingPlan.Pro, MonthlyEventLimit = BillingPlanLimits.Pro, BillingStatus = "Active" }),
            new FakeStripeGateway());

        var status = await service.GetBillingStatusAsync("tenant-1");

        Assert.NotNull(status);
        Assert.Equal(BillingPlan.Pro, status!.Plan);
        Assert.Equal(BillingPlanLimits.Pro, status.MonthlyEventLimit);
    }

    [Fact]
    public async Task CheckoutCompleted_UpdatesTenantBilling()
    {
        var tenantRepo = new InMemoryTenantRepository(new Tenant { Id = "tenant-1", Plan = BillingPlan.Free, MonthlyEventLimit = BillingPlanLimits.Free });
        var stripe = new FakeStripeGateway
        {
            WebhookEvent = new Event
            {
                Type = Events.CheckoutSessionCompleted,
                Data = new EventData
                {
                    Object = new Session
                    {
                        CustomerId = "cus_123",
                        SubscriptionId = "sub_123",
                        Metadata = new Dictionary<string, string>
                        {
                            ["tenantId"] = "tenant-1",
                            ["plan"] = "Starter",
                        },
                    },
                },
            },
        };

        var service = CreateService(tenantRepo, stripe);

        await service.HandleStripeWebhookAsync("{}", "signature");

        var tenant = await tenantRepo.GetByIdAsync("tenant-1");
        Assert.NotNull(tenant);
        Assert.Equal("cus_123", tenant!.StripeCustomerId);
        Assert.Equal("sub_123", tenant.StripeSubscriptionId);
        Assert.Equal(BillingPlan.Starter, tenant.Plan);
        Assert.Equal(BillingPlanLimits.Starter, tenant.MonthlyEventLimit);
    }

    [Fact]
    public async Task SubscriptionDeleted_DowngradesToFreePlan()
    {
        var tenantRepo = new InMemoryTenantRepository(new Tenant
        {
            Id = "tenant-1",
            Plan = BillingPlan.Pro,
            MonthlyEventLimit = BillingPlanLimits.Pro,
            StripeCustomerId = "cus_123",
            StripeSubscriptionId = "sub_123",
            BillingStatus = "Active",
        });

        var stripe = new FakeStripeGateway
        {
            WebhookEvent = new Event
            {
                Type = Events.CustomerSubscriptionDeleted,
                Data = new EventData
                {
                    Object = new StripeSubscription
                    {
                        CustomerId = "cus_123",
                        Id = "sub_123",
                    },
                },
            },
        };

        var service = CreateService(tenantRepo, stripe);

        await service.HandleStripeWebhookAsync("{}", "signature");

        var tenant = await tenantRepo.GetByIdAsync("tenant-1");
        Assert.NotNull(tenant);
        Assert.Equal(BillingPlan.Free, tenant!.Plan);
        Assert.Equal(BillingPlanLimits.Free, tenant.MonthlyEventLimit);
        Assert.Equal("Canceled", tenant.BillingStatus);
    }

    [Fact]
    public async Task PaymentFailed_SetsPaymentFailedStatus()
    {
        var tenantRepo = new InMemoryTenantRepository(new Tenant
        {
            Id = "tenant-1",
            StripeCustomerId = "cus_123",
            BillingStatus = "Active",
        });

        var stripe = new FakeStripeGateway
        {
            WebhookEvent = new Event
            {
                Type = Events.InvoicePaymentFailed,
                Data = new EventData
                {
                    Object = new Invoice
                    {
                        CustomerId = "cus_123",
                    },
                },
            },
        };

        var service = CreateService(tenantRepo, stripe);

        await service.HandleStripeWebhookAsync("{}", "signature");

        var tenant = await tenantRepo.GetByIdAsync("tenant-1");
        Assert.NotNull(tenant);
        Assert.Equal("PaymentFailed", tenant!.BillingStatus);
    }

    private static BillingService CreateService(InMemoryTenantRepository tenantRepository, FakeStripeGateway stripe)
        => new(
            tenantRepository,
            new CreateCheckoutSessionRequestDtoValidator(),
            Options.Create(new StripeSettings
            {
                SecretKey = "sk_test",
                WebhookSecret = "whsec",
                StarterPriceId = "price_starter",
                ProPriceId = "price_pro",
                EnterprisePriceId = "price_enterprise",
                SuccessUrl = "http://localhost:3000/billing/success",
                CancelUrl = "http://localhost:3000/billing/cancel",
            }),
            stripe,
            NullLogger<BillingService>.Instance);

    private sealed class InMemoryTenantRepository(Tenant tenant) : IMongoRepository<Tenant>
    {
        private readonly List<Tenant> tenants = new() { tenant };

        public Task<Tenant?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(tenants.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<Tenant>> FindAsync(System.Linq.Expressions.Expression<Func<Tenant, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Tenant>>(tenants.AsQueryable().Where(predicate).ToList());

        public Task<Tenant?> FirstOrDefaultAsync(System.Linq.Expressions.Expression<Func<Tenant, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(tenants.AsQueryable().FirstOrDefault(predicate));

        public Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Tenant>>(tenants);

        public Task AddAsync(Tenant entity, CancellationToken cancellationToken = default)
        {
            tenants.Add(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Tenant entity, CancellationToken cancellationToken = default)
        {
            var index = tenants.FindIndex(x => x.Id == entity.Id);
            if (index >= 0)
            {
                tenants[index] = entity;
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeStripeGateway : IStripeGateway
    {
        public SessionCreateOptions? LastSessionOptions { get; private set; }

        public Event? WebhookEvent { get; set; }

        public Task<Customer> CreateCustomerAsync(CustomerCreateOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(new Customer { Id = "cus_created" });

        public Task<Session> CreateCheckoutSessionAsync(SessionCreateOptions options, CancellationToken cancellationToken = default)
        {
            LastSessionOptions = options;
            return Task.FromResult(new Session { Id = "cs_123", Url = "https://checkout.stripe.com/session/cs_123" });
        }

        public Event ConstructWebhookEvent(string payload, string signatureHeader, string webhookSecret)
            => WebhookEvent ?? throw new StripeException("Missing webhook event for test.");
    }
}
