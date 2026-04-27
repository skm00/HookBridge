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
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoDbSettings>(configuration.GetSection("MongoDb"));
        services.Configure<KafkaSettings>(configuration.GetSection("Kafka"));
        services.Configure<StripeSettings>(configuration.GetSection("Stripe"));
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.Configure<ElasticSettings>(configuration.GetSection("Elastic"));
        services.Configure<ElasticApmSettings>(configuration.GetSection("ElasticApm"));

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
        services.AddSingleton<ITracingService, ElasticApmTracingService>();
        services.AddSingleton<IKafkaProducer, KafkaProducer>();
        services.AddSingleton<IKafkaConsumer, KafkaConsumer>();
        services.AddSingleton<IKafkaAdminService, KafkaAdminServicePlaceholder>();
        services.AddHttpClient();
        services.AddSingleton<IOAuthTokenService, OAuthTokenService>();
        services.AddScoped<IWebhookAuthenticationHandler, WebhookAuthenticationHandler>();
        services.AddScoped<IWebhookDeliveryClient, WebhookDeliveryClient>();
        services.AddScoped<IStripeGateway, StripeGateway>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped(typeof(IMongoRepository<>), typeof(MongoRepository<>));
        services.AddScoped<IDeliveryAttemptRepository, DeliveryAttemptRepository>();
        services.AddScoped<IFailedEventRepository, FailedEventRepository>();
        services.AddScoped<IUsageMetricRepository, UsageMetricRepository>();
        services.AddHostedService<MongoIndexInitializerHostedService>();

        return services;
    }
}
