using FluentValidation;
using HookBridge.Application.Configuration;
using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Services;
using HookBridge.Application.Validation.Subscriptions;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class SubscriptionServiceTests
{
    [Fact]
    public async Task CreateSubscription_Success()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        await tenantRepo.AddAsync(new Tenant { Id = "tenant-2", Name = "Two", Slug = "two", Status = TenantStatus.Active, CreatedAt = DateTime.UtcNow });
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        var result = await service.CreateAsync("tenant-1", new CreateSubscriptionRequestDto
        {
            EventType = "order.created",
            TargetUrl = "https://example.com/hooks",
            RetryPolicy = new RetryPolicyDto
            {
                MaxAttempts = 5,
                InitialDelaySeconds = 45,
                BackoffType = "Fixed",
            },
            TimeoutSeconds = 40,
        });

        Assert.Equal("subscription-1", result.Id);
        Assert.True(result.IsActive);
        Assert.Equal("order.created", result.EventType);
    }

    [Fact]
    public async Task CreateSubscription_WritesAuditLog()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var audit = new RecordingAuditLogService();
        var service = CreateService(subscriptionRepo, tenantRepo, auditLogService: audit);

        await service.CreateAsync("tenant-1", BuildValidRequest());

        Assert.Equal("SubscriptionCreated", Assert.Single(audit.Logged).Action);
    }

    [Fact]
    public async Task UpdateSubscription_WritesAuditLog()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var audit = new RecordingAuditLogService();
        var service = CreateService(subscriptionRepo, tenantRepo, auditLogService: audit);
        var created = await service.CreateAsync("tenant-1", BuildValidRequest());
        audit.Logged.Clear();

        await service.UpdateAsync("tenant-1", created.Id, new UpdateSubscriptionRequestDto { EventType = "order.updated" });

        Assert.Equal("SubscriptionUpdated", Assert.Single(audit.Logged).Action);
    }

    [Fact]
    public async Task DeleteSubscription_WritesAuditLog()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var audit = new RecordingAuditLogService();
        var service = CreateService(subscriptionRepo, tenantRepo, auditLogService: audit);
        var created = await service.CreateAsync("tenant-1", BuildValidRequest());
        audit.Logged.Clear();

        await service.DeleteAsync("tenant-1", created.Id);

        Assert.Equal("SubscriptionDeleted", Assert.Single(audit.Logged).Action);
    }

    [Fact]
    public async Task CreateSubscription_FailsIfTenantNotFound()
    {
        var tenantRepo = new InMemoryRepository<Tenant>();
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateAsync("tenant-1", BuildValidRequest()));
    }

    [Fact]
    public async Task CreateSubscription_FailsIfTenantDisabled()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Disabled);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync("tenant-1", BuildValidRequest()));
    }

    [Fact]
    public async Task InvalidTargetUrl_ValidationFails()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        var request = BuildValidRequest();
        request.TargetUrl = "/not-absolute";

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync("tenant-1", request));
    }

    [Fact]
    public async Task DuplicateHeaderNames_ValidationFails()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        var request = BuildValidRequest();
        request.Headers =
        [
            new KeyValueDto { Name = "x-signature", Value = "1" },
            new KeyValueDto { Name = "X-Signature", Value = "2" },
        ];

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync("tenant-1", request));
    }


    [Fact]
    public async Task TargetUrl_NonHttpScheme_FailsValidation()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        var request = BuildValidRequest();
        request.TargetUrl = "ftp://example.com/hook";

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync("tenant-1", request));
    }

    [Fact]
    public async Task TargetUrl_PrivateAddressInProduction_FailsValidation()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var createValidator = new CreateSubscriptionRequestDtoValidator(
            new TestHostEnvironment(Environments.Production),
            Options.Create(new SecuritySettings { AllowPrivateNetworkTargetUrls = false }));
        var updateValidator = new UpdateSubscriptionRequestDtoValidator(
            new TestHostEnvironment(Environments.Production),
            Options.Create(new SecuritySettings { AllowPrivateNetworkTargetUrls = false }));
        var service = CreateService(subscriptionRepo, tenantRepo, createValidator, updateValidator);

        var request = BuildValidRequest();
        request.TargetUrl = "http://127.0.0.1:8080/hook";

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync("tenant-1", request));
    }

    [Fact]
    public async Task HeaderCrLfInjection_FailsValidation()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        var request = BuildValidRequest();
        request.Headers = [new KeyValueDto { Name = "x-test", Value = "ok\r\nmalicious:true" }];

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync("tenant-1", request));
    }

    [Fact]
    public async Task TooManyCustomHeaders_FailsValidation()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        var request = BuildValidRequest();
        request.Headers = Enumerable.Range(1, 31)
            .Select(index => new KeyValueDto { Name = $"x-test-{index}", Value = "value" })
            .ToList();

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync("tenant-1", request));
    }

    [Fact]
    public async Task RestrictedHeader_FailsValidation()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        var request = BuildValidRequest();
        request.Headers = [new KeyValueDto { Name = "Host", Value = "example.com" }];

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync("tenant-1", request));
    }

    [Fact]
    public async Task OAuthTokenUrl_RequiresHttpsInProduction()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var createValidator = new CreateSubscriptionRequestDtoValidator(
            new TestHostEnvironment(Environments.Production),
            Options.Create(new SecuritySettings { AllowPrivateNetworkTargetUrls = false }));
        var updateValidator = new UpdateSubscriptionRequestDtoValidator(
            new TestHostEnvironment(Environments.Production),
            Options.Create(new SecuritySettings { AllowPrivateNetworkTargetUrls = false }));
        var service = CreateService(subscriptionRepo, tenantRepo, createValidator, updateValidator);

        var request = BuildValidRequest();
        request.Authentication = new AuthenticationDto
        {
            Type = "OAuth2ClientCredentials",
            OAuth2 = new OAuth2ClientCredentialsDto
            {
                TokenUrl = "http://auth.example.com/token",
                ClientId = "client-id",
                ClientSecret = "client-secret",
            },
        };

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync("tenant-1", request));
    }

    [Fact]
    public async Task DefaultRetryPolicy_IsApplied()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        var request = BuildValidRequest();
        request.RetryPolicy = null;
        request.TimeoutSeconds = null;

        var result = await service.CreateAsync("tenant-1", request);

        Assert.Equal(3, result.RetryPolicy.MaxAttempts);
        Assert.Equal(30, result.RetryPolicy.InitialDelaySeconds);
        Assert.Equal("Exponential", result.RetryPolicy.BackoffType);
        Assert.Equal(30, result.TimeoutSeconds);
    }

    [Fact]
    public async Task GetSubscriptionById_ReturnsSubscription()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);
        var created = await service.CreateAsync("tenant-1", BuildValidRequest());

        var fetched = await service.GetByIdAsync("tenant-1", created.Id);

        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched!.Id);
    }

    [Fact]
    public async Task SearchSubscription_ByTenantId()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        await tenantRepo.AddAsync(new Tenant { Id = "tenant-2", Name = "Two", Slug = "two", Status = TenantStatus.Active, CreatedAt = DateTime.UtcNow });

        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);
        await service.CreateAsync("tenant-1", BuildValidRequest());

        var second = BuildValidRequest();
                second.TargetUrl = "https://example.com/two";
        second.EventType = "order.updated";
        await service.CreateAsync("tenant-2", second);

        var results = await service.SearchAsync(new SubscriptionSearchRequestDto { TenantId = "tenant-1" });

        Assert.Single(results.Items);
        Assert.Equal("https://example.com/hooks", results.Items[0].TargetUrl);
    }

    [Fact]
    public async Task SearchSubscription_ByEventType()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        await service.CreateAsync("tenant-1", BuildValidRequest());

        var second = BuildValidRequest();
        second.EventType = "order.cancelled";
        second.TargetUrl = "https://example.com/cancel";
        await service.CreateAsync("tenant-2", second);

        var results = await service.SearchAsync(new SubscriptionSearchRequestDto { EventType = "order.cancelled" });

        Assert.Single(results.Items);
        Assert.Equal("order.cancelled", results.Items[0].EventType);
    }

    [Fact]
    public async Task SecretValues_AreMaskedInResponse()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        var request = BuildValidRequest();
        request.Authentication = new AuthenticationDto
        {
            Type = "Basic",
            Basic = new BasicAuthDto
            {
                Username = "user",
                Password = "super-secret",
            },
        };

        var created = await service.CreateAsync("tenant-1", request);

        Assert.NotNull(created.Authentication);
        Assert.Equal("********", created.Authentication!.Basic!.Password);
    }

    [Fact]
    public async Task UpdateSubscription_Success()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);
        var created = await service.CreateAsync("tenant-1", BuildValidRequest());

        var updated = await service.UpdateAsync("tenant-1", created.Id, new UpdateSubscriptionRequestDto
        {
            EventType = "order.updated",
            TargetUrl = "https://example.com/updated",
            TimeoutSeconds = 60,
        });

        Assert.NotNull(updated);
        Assert.Equal("order.updated", updated!.EventType);
        Assert.Equal("https://example.com/updated", updated.TargetUrl);
        Assert.Equal(60, updated.TimeoutSeconds);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), updated.UpdatedAt);
    }

    [Fact]
    public async Task UpdateSubscription_OnlyProvidedFieldsAreChanged()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);
        var created = await service.CreateAsync("tenant-1", BuildValidRequest());

        var updated = await service.UpdateAsync("tenant-1", created.Id, new UpdateSubscriptionRequestDto
        {
            TargetUrl = "https://example.com/new-target",
        });

        Assert.NotNull(updated);
        Assert.Equal("order.created", updated!.EventType);
        Assert.Equal("https://example.com/new-target", updated.TargetUrl);
        Assert.Equal(30, updated.TimeoutSeconds);
    }

    [Fact]
    public async Task UpdateSubscription_ReturnsNull_WhenNotFound()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        var updated = await service.UpdateAsync("tenant-1", "missing", new UpdateSubscriptionRequestDto
        {
            EventType = "order.updated",
        });

        Assert.Null(updated);
    }

    [Fact]
    public async Task UpdateSubscription_InvalidTargetUrl_FailsValidation()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);
        var created = await service.CreateAsync("tenant-1", BuildValidRequest());

        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync("tenant-1", created.Id, new UpdateSubscriptionRequestDto
        {
            TargetUrl = "/invalid",
        }));
    }

    [Fact]
    public async Task UpdateSubscription_DuplicateHeaders_FailsValidation()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);
        var created = await service.CreateAsync("tenant-1", BuildValidRequest());

        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync("tenant-1", created.Id, new UpdateSubscriptionRequestDto
        {
            Headers =
            [
                new KeyValueDto { Name = "x-test", Value = "1" },
                new KeyValueDto { Name = "X-Test", Value = "2" },
            ],
        }));
    }

    [Fact]
    public async Task DeleteSubscription_Success()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);
        var created = await service.CreateAsync("tenant-1", BuildValidRequest());

        var deleted = await service.DeleteAsync("tenant-1", created.Id);
        var fetched = await service.GetByIdAsync("tenant-1", created.Id);

        Assert.True(deleted);
        Assert.Null(fetched);
    }

    [Fact]
    public async Task DeleteSubscription_ReturnsFalse_WhenNotFound()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        var deleted = await service.DeleteAsync("tenant-1", "missing");

        Assert.False(deleted);
    }

    [Fact]
    public async Task DisableSubscription_SetsInactiveAndDisabledAt()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);
        var created = await service.CreateAsync("tenant-1", BuildValidRequest());

        var disabled = await service.DisableAsync("tenant-1", created.Id);
        var fetched = await service.GetByIdAsync("tenant-1", created.Id);

        Assert.True(disabled);
        Assert.NotNull(fetched);
        Assert.False(fetched!.IsActive);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), fetched.DisabledAt);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), fetched.UpdatedAt);
    }

    [Fact]
    public async Task EnableSubscription_SetsActiveAndClearsDisabledAt()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);
        var created = await service.CreateAsync("tenant-1", BuildValidRequest());
        await service.DisableAsync("tenant-1", created.Id);

        var enabled = await service.EnableAsync("tenant-1", created.Id);
        var fetched = await service.GetByIdAsync("tenant-1", created.Id);

        Assert.True(enabled);
        Assert.NotNull(fetched);
        Assert.True(fetched!.IsActive);
        Assert.Null(fetched.DisabledAt);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), fetched.UpdatedAt);
    }

    [Fact]
    public async Task SecretValues_AreMaskedAfterUpdate()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);
        var created = await service.CreateAsync("tenant-1", BuildValidRequest());

        var updated = await service.UpdateAsync("tenant-1", created.Id, new UpdateSubscriptionRequestDto
        {
            Authentication = new AuthenticationDto
            {
                Type = "ApiKeyHeader",
                ApiKeyHeader = new ApiKeyHeaderDto
                {
                    HeaderName = "x-api-key",
                    HeaderValue = "new-super-secret",
                },
            },
        });

        Assert.NotNull(updated);
        Assert.NotNull(updated!.Authentication);
        Assert.Equal("********", updated.Authentication!.ApiKeyHeader!.HeaderValue);
    }


    [Fact]
    public async Task CreateSubscription_StoresEncryptedBasicPassword()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var encryption = new FakeSecretEncryptionService();
        var service = CreateService(subscriptionRepo, tenantRepo, encryptionService: encryption);

        var request = BuildValidRequest();
        request.Authentication = new AuthenticationDto
        {
            Type = "Basic",
            Basic = new BasicAuthDto { Username = "user", Password = "plain-password" },
        };

        var created = await service.CreateAsync("tenant-1", request);
        var stored = await subscriptionRepo.GetByIdAsync(created.Id);

        Assert.True(encryption.IsEncrypted(stored!.Authentication!.Basic!.Password));
    }

    [Fact]
    public async Task CreateSubscription_StoresEncryptedOAuthClientSecret()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var encryption = new FakeSecretEncryptionService();
        var service = CreateService(subscriptionRepo, tenantRepo, encryptionService: encryption);

        var request = BuildValidRequest();
        request.Authentication = new AuthenticationDto
        {
            Type = "OAuth2ClientCredentials",
            OAuth2 = new OAuth2ClientCredentialsDto
            {
                TokenUrl = "https://auth.example.com/token",
                ClientId = "cid",
                ClientSecret = "oauth-secret",
            },
        };

        var created = await service.CreateAsync("tenant-1", request);
        var stored = await subscriptionRepo.GetByIdAsync(created.Id);

        Assert.True(encryption.IsEncrypted(stored!.Authentication!.OAuth2!.ClientSecret));
    }

    [Fact]
    public async Task CreateSubscription_StoresEncryptedApiKeyHeaderValue()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var encryption = new FakeSecretEncryptionService();
        var service = CreateService(subscriptionRepo, tenantRepo, encryptionService: encryption);

        var request = BuildValidRequest();
        request.Authentication = new AuthenticationDto
        {
            Type = "ApiKeyHeader",
            ApiKeyHeader = new ApiKeyHeaderDto { HeaderName = "x-api-key", HeaderValue = "api-secret" },
        };

        var created = await service.CreateAsync("tenant-1", request);
        var stored = await subscriptionRepo.GetByIdAsync(created.Id);

        Assert.True(encryption.IsEncrypted(stored!.Authentication!.ApiKeyHeader!.HeaderValue));
    }

    [Fact]
    public async Task CreateSubscription_StoresEncryptedHmacSecret()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var encryption = new FakeSecretEncryptionService();
        var service = CreateService(subscriptionRepo, tenantRepo, encryptionService: encryption);

        var request = BuildValidRequest();
        request.Authentication = new AuthenticationDto
        {
            Type = "HmacSignature",
            HmacSignature = new HmacSignatureDto { Secret = "hmac-secret", HeaderName = "x-sign", Algorithm = "HMACSHA256" },
        };

        var created = await service.CreateAsync("tenant-1", request);
        var stored = await subscriptionRepo.GetByIdAsync(created.Id);

        Assert.True(encryption.IsEncrypted(stored!.Authentication!.HmacSignature!.Secret));
    }

    [Fact]
    public async Task UpdateSubscription_MaskedSecret_KeepsExistingEncryptedValue()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var encryption = new FakeSecretEncryptionService();
        var service = CreateService(subscriptionRepo, tenantRepo, encryptionService: encryption);

        var created = await service.CreateAsync("tenant-1", new CreateSubscriptionRequestDto
        {
            EventType = "order.created",
            TargetUrl = "https://example.com/hooks",
            RetryPolicy = new RetryPolicyDto { MaxAttempts = 3, InitialDelaySeconds = 30, BackoffType = "Exponential" },
            TimeoutSeconds = 30,
            Authentication = new AuthenticationDto
            {
                Type = "Basic",
                Basic = new BasicAuthDto { Username = "user", Password = "first-secret" },
            },
        });

        var before = (await subscriptionRepo.GetByIdAsync(created.Id))!.Authentication!.Basic!.Password;

        await service.UpdateAsync("tenant-1", created.Id, new UpdateSubscriptionRequestDto
        {
            Authentication = new AuthenticationDto
            {
                Type = "Basic",
                Basic = new BasicAuthDto { Username = "user", Password = "********" },
            },
        });

        var after = (await subscriptionRepo.GetByIdAsync(created.Id))!.Authentication!.Basic!.Password;
        Assert.Equal(before, after);
    }

    private static CreateSubscriptionRequestDto BuildValidRequest() => new()
    {
        EventType = "order.created",
        TargetUrl = "https://example.com/hooks",
        RetryPolicy = new RetryPolicyDto
        {
            MaxAttempts = 3,
            InitialDelaySeconds = 30,
            BackoffType = "Exponential",
        },
        TimeoutSeconds = 30,
    };

    private static SubscriptionService CreateService(
        InMemoryRepository<Subscription> subscriptionRepo,
        InMemoryRepository<Tenant> tenantRepo,
        CreateSubscriptionRequestDtoValidator? createValidator = null,
        UpdateSubscriptionRequestDtoValidator? updateValidator = null,
        ISecretEncryptionService? encryptionService = null,
        IAuditLogService? auditLogService = null)
    {
        return new SubscriptionService(
            subscriptionRepo,
            tenantRepo,
            auditLogService ?? new RecordingAuditLogService(),
            new FixedGuidGenerator(),
            new FixedDateTimeProvider(),
            createValidator ?? new CreateSubscriptionRequestDtoValidator(),
            updateValidator ?? new UpdateSubscriptionRequestDtoValidator(),
            encryptionService ?? new FakeSecretEncryptionService(),
            NullLogger<SubscriptionService>.Instance);
    }

    private sealed class RecordingAuditLogService : IAuditLogService
    {
        public List<AuditLog> Logged { get; } = [];
        public Task LogAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
        {
            Logged.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task<HookBridge.Application.DTOs.Common.PagedResponseDto<HookBridge.Application.DTOs.AuditLogs.AuditLogResponseDto>> SearchAsync(HookBridge.Application.DTOs.AuditLogs.AuditLogSearchRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(HookBridge.Application.DTOs.Common.PagedResponseDto<HookBridge.Application.DTOs.AuditLogs.AuditLogResponseDto>.Create([], 1, 50, 0));
        public Task<HookBridge.Application.DTOs.AuditLogs.AuditLogResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult<HookBridge.Application.DTOs.AuditLogs.AuditLogResponseDto?>(null);
    }

    private static InMemoryRepository<Tenant> BuildTenantRepo(TenantStatus status)
    {
        var repo = new InMemoryRepository<Tenant>();
        repo.AddAsync(new Tenant
        {
            Id = "tenant-1",
            Name = "Acme",
            Slug = "acme",
            Status = status,
            CreatedAt = DateTime.UtcNow,
        }).GetAwaiter().GetResult();

        return repo;
    }

    private sealed class FixedGuidGenerator : IGuidGenerator
    {
        private int _counter;

        public string NewGuid()
        {
            _counter++;
            return $"subscription-{_counter}";
        }
    }

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }


    private sealed class FakeSecretEncryptionService : ISecretEncryptionService
    {
        private const string Prefix = "enc:v1:fake:";

        public string Encrypt(string plainText)
            => IsEncrypted(plainText)
                ? plainText
                : Prefix + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plainText));

        public string Decrypt(string cipherText)
            => IsEncrypted(cipherText)
                ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cipherText[Prefix.Length..]))
                : cipherText;

        public bool IsEncrypted(string value)
            => !string.IsNullOrWhiteSpace(value)
               && value.StartsWith(Prefix, StringComparison.Ordinal);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "HookBridge.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(AppContext.BaseDirectory);
    }

    private sealed class InMemoryRepository<T> : IMongoRepository<T>
        where T : BaseEntity
    {
        private readonly List<T> _items = [];

        public Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<T>> FindAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return Task.FromResult<IReadOnlyList<T>>(_items.Where(compiled).ToList());
        }

        public Task<T?> FirstOrDefaultAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return Task.FromResult(_items.FirstOrDefault(compiled));
        }

        public Task<(IReadOnlyList<T> Items, long TotalCount)> QueryAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate, MongoDB.Driver.SortDefinition<T> sort, int skip, int limit, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            var filtered = _items.Where(compiled).ToList();
            var paged = filtered.Skip(skip).Take(limit).ToList();
            return Task.FromResult<(IReadOnlyList<T>, long)>((paged, filtered.LongCount()));
        }

        public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<T>>(_items.ToList());

        public Task AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            _items.Add(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            var index = _items.FindIndex(x => x.Id == entity.Id);
            if (index >= 0)
            {
                _items[index] = entity;
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            _items.RemoveAll(x => x.Id == id);
            return Task.CompletedTask;
        }
    }
}
