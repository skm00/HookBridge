using HookBridge.Application.Configuration;
using HookBridge.Application.Services;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Interfaces.Security;
using HookBridge.Application.Messaging;
using HookBridge.Infrastructure.Configuration;
using HookBridge.Infrastructure.Persistence;
using HookBridge.Infrastructure.Persistence.Repositories;
using HookBridge.Infrastructure.Persistence.Indexes;
using HookBridge.Infrastructure.Services;
using HookBridge.Infrastructure.Services.Billing;
using HookBridge.Infrastructure.Services.Auth;
using HookBridge.Infrastructure.Services.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.Infrastructure.DependencyInjection;

/// <summary>
/// Registers infrastructure-layer dependencies.
/// </summary>
public static class InfrastructureServiceRegistration
{
    /// <summary>
    /// Adds infrastructure services including MongoDB, utility providers, and repositories.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="requireKafkaConsumerGroupId">Whether Kafka ConsumerGroupId is required for startup.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        bool requireKafkaConsumerGroupId = false)
    {
        var isProduction = environment.IsProduction();

        services.AddValidatedOptions<MongoDbSettings>(
            configuration,
            "MongoDb",
            settings =>
            [
                Required(settings.ConnectionString, "ConnectionString"),
                Required(settings.DatabaseName, "DatabaseName"),
            ]);

        services.AddValidatedOptions<KafkaSettings>(
            configuration,
            "Kafka",
            settings =>
            [
                Required(settings.BootstrapServers, "BootstrapServers"),
                requireKafkaConsumerGroupId ? Required(settings.ConsumerGroupId, "ConsumerGroupId") : null,
                Positive(settings.MessageTimeoutMs, "MessageTimeoutMs"),
            ]);

        services.AddValidatedOptions<JwtSettings>(
            configuration,
            "Jwt",
            settings =>
            [
                Required(settings.Issuer, "Issuer"),
                Required(settings.Audience, "Audience"),
                Required(settings.Secret, "Secret"),
                MinLength(settings.Secret, "Secret", 32),
                Positive(settings.ExpiryMinutes, "ExpiryMinutes"),
            ]);

        services.AddValidatedOptions<StripeSettings>(
            configuration,
            "Stripe",
            settings =>
            [
                isProduction ? Required(settings.SecretKey, "SecretKey") : null,
                isProduction ? Required(settings.WebhookSecret, "WebhookSecret") : null,
                isProduction ? Required(settings.StarterPriceId, "StarterPriceId") : null,
                isProduction ? Required(settings.ProPriceId, "ProPriceId") : null,
                Required(settings.SuccessUrl, "SuccessUrl"),
                Required(settings.CancelUrl, "CancelUrl"),
            ]);

        services.AddValidatedOptions<ElasticSettings>(
            configuration,
            "Elastic",
            settings =>
            [
                settings.EnableElasticsearchSink ? Required(settings.ElasticsearchUrl, "ElasticsearchUrl") : null,
                Required(settings.ServiceName, "ServiceName"),
                Required(settings.Environment, "Environment"),
            ]);

        services.AddValidatedOptions<ElasticApmSettings>(
            configuration,
            "ElasticApm",
            settings =>
            [
                settings.Enabled ? Required(settings.ServerUrl, "ServerUrl") : null,
                settings.Enabled ? Required(settings.ServiceName, "ServiceName") : null,
                settings.Enabled ? Required(settings.Environment, "Environment") : null,
            ]);

        services.AddValidatedOptions<SecuritySettings>(
            configuration,
            "Security",
            _ => []);

        services.AddValidatedOptions<FeatureFlagsSettings>(
            configuration,
            "FeatureFlags",
            _ => []);

        services.AddValidatedOptions<EncryptionSettings>(
            configuration,
            "Encryption",
            settings =>
            [
                isProduction ? Required(settings.MasterKey, "MasterKey") : null,
                isProduction ? MinLength(settings.MasterKey, "MasterKey", 32) : null,
            ]);

        services.AddValidatedOptions<EmailSettings>(
            configuration,
            "Email",
            settings =>
            [
                settings.Enabled ? Required(settings.Provider, "Provider") : null,
                settings.Enabled ? Required(settings.SmtpHost, "SmtpHost") : null,
                settings.Enabled ? Positive(settings.SmtpPort, "SmtpPort") : null,
                settings.Enabled ? Required(settings.FromEmail, "FromEmail") : null,
            ]);
        services.AddValidatedOptions<DemoDataSettings>(
            configuration,
            "DemoData",
            settings =>
            [
                settings.Enabled ? Required(settings.AdminEmail, "AdminEmail") : null,
                settings.Enabled ? Required(settings.AdminPassword, "AdminPassword") : null,
                settings.Enabled ? Required(settings.TenantName, "TenantName") : null,
                settings.Enabled ? Required(settings.TenantSlug, "TenantSlug") : null,
            ]);

        services.AddValidatedOptions<DataRetentionSettings>(
            configuration,
            "DataRetention",
            settings =>
            [
                Positive(settings.IncomingEventsDays, "IncomingEventsDays"),
                Positive(settings.DeliveryLogsDays, "DeliveryLogsDays"),
                Positive(settings.FailedEventsDays, "FailedEventsDays"),
                Positive(settings.AuditLogsDays, "AuditLogsDays"),
                Positive(settings.NotificationsDays, "NotificationsDays"),
            ]);

        services.PostConfigure<SecuritySettings>(settings =>
        {
            if (string.IsNullOrWhiteSpace(configuration["Security:AllowPrivateNetworkTargetUrls"]))
            {
                settings.AllowPrivateNetworkTargetUrls = environment.IsDevelopment();
            }
        });

        services.AddSingleton<IMongoClient>(serviceProvider =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<MongoDbSettings>>().Value;
            return new MongoClient(settings.ConnectionString);
        });

        services.AddSingleton<IMongoDatabase>(serviceProvider =>
        {
            var client = serviceProvider.GetRequiredService<IMongoClient>();
            var settings = serviceProvider.GetRequiredService<IOptions<MongoDbSettings>>().Value;
            return client.GetDatabase(settings.DatabaseName);
        });

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IApiKeyGenerator, ApiKeyGenerator>();
        services.AddSingleton<IApiKeyHasher, ApiKeyHasher>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IGuidGenerator, GuidGenerator>();
        services.AddSingleton<ISecretEncryptionService, SecretEncryptionService>();
        services.AddSingleton<IFeatureFlagService, FeatureFlagService>();
        services.AddSingleton<ITracingService, ElasticApmTracingService>();
        services.AddSingleton<IKafkaProducer, KafkaProducer>();
        services.AddSingleton<IKafkaConsumer, KafkaConsumer>();
        services.AddSingleton<IKafkaAdminService, KafkaAdminServicePlaceholder>();
        services.AddHttpClient();
        services.AddSingleton<IOAuthTokenService, OAuthTokenService>();
        services.AddScoped<IWebhookAuthenticationHandler, WebhookAuthenticationHandler>();
        services.AddScoped<IWebhookDeliveryClient, WebhookDeliveryClient>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IStripeGateway, StripeGateway>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IDemoDataSeeder, DemoDataSeeder>();
        services.AddScoped<IDataCleanupService, DataCleanupService>();
        services.AddScoped(typeof(IMongoRepository<>), typeof(MongoRepository<>));
        services.AddScoped<IDeliveryAttemptRepository, DeliveryAttemptRepository>();
        services.AddScoped<IFailedEventRepository, FailedEventRepository>();
        services.AddScoped<IUsageMetricRepository, UsageMetricRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddHostedService<MongoIndexInitializerHostedService>();

        return services;
    }

    private static string? Required(string? value, string fieldName)
        => string.IsNullOrWhiteSpace(value)
            ? $"{fieldName} is required."
            : null;

    private static string? Positive(int value, string fieldName)
        => value <= 0
            ? $"{fieldName} must be greater than 0."
            : null;

    private static string? MinLength(string? value, string fieldName, int minLength)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length < minLength
                ? $"{fieldName} must be at least {minLength} characters long."
                : null;
}
