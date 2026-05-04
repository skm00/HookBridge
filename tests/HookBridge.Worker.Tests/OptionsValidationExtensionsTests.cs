using Xunit;
using HookBridge.Infrastructure.Configuration;
using HookBridge.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HookBridge.Worker.Tests;

public class OptionsValidationExtensionsTests
{
    [Fact]
    public void MissingMongoConnectionString_FailsValidation()
    {
        using var serviceProvider = BuildServiceProvider(
            new Dictionary<string, string?>
            {
                ["MongoDb:DatabaseName"] = "hookbridge",
            },
            Environments.Production);

        var exception = Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptions<MongoDbSettings>>().Value);

        Assert.Contains("MongoDb:ConnectionString is required.", exception.Failures);
    }

    [Fact]
    public void JwtSecretShorterThan32Chars_FailsValidation()
    {
        using var serviceProvider = BuildServiceProvider(
            new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "issuer",
                ["Jwt:Audience"] = "audience",
                ["Jwt:Secret"] = "short-secret",
                ["Jwt:ExpiryMinutes"] = "60",
            },
            Environments.Production);

        var exception = Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptions<JwtSettings>>().Value);

        Assert.Contains("Jwt:Secret must be at least 32 characters long.", exception.Failures);
    }

    [Fact]
    public void StripeSecrets_NotRequiredInDevelopment()
    {
        using var serviceProvider = BuildServiceProvider(
            new Dictionary<string, string?>
            {
                ["Stripe:SuccessUrl"] = "https://localhost/success",
                ["Stripe:CancelUrl"] = "https://localhost/cancel",
            },
            Environments.Development);

        var settings = serviceProvider.GetRequiredService<IOptions<StripeSettings>>().Value;

        Assert.Equal("https://localhost/success", settings.SuccessUrl);
    }

    [Fact]
    public void StripeSecrets_RequiredInProduction()
    {
        using var serviceProvider = BuildServiceProvider(
            new Dictionary<string, string?>
            {
                ["Stripe:SuccessUrl"] = "https://hookbridge.app/success",
                ["Stripe:CancelUrl"] = "https://hookbridge.app/cancel",
            },
            Environments.Production);

        var exception = Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptions<StripeSettings>>().Value);

        Assert.Contains("Stripe:SecretKey is required.", exception.Failures);
        Assert.Contains("Stripe:WebhookSecret is required.", exception.Failures);
    }

    [Fact]
    public void ElasticUrlRequired_WhenSinkEnabled()
    {
        using var serviceProvider = BuildServiceProvider(
            new Dictionary<string, string?>
            {
                ["Elastic:EnableElasticsearchSink"] = "true",
                ["Elastic:ServiceName"] = "hookbridge",
                ["Elastic:Environment"] = "Production",
            },
            Environments.Production);

        var exception = Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptions<ElasticSettings>>().Value);

        Assert.Contains("Elastic:ElasticsearchUrl is required.", exception.Failures);
    }

    [Fact]
    public void ElasticApmServerUrlRequired_WhenApmEnabled()
    {
        using var serviceProvider = BuildServiceProvider(
            new Dictionary<string, string?>
            {
                ["ElasticApm:Enabled"] = "true",
                ["ElasticApm:ServiceName"] = "hookbridge-worker",
                ["ElasticApm:Environment"] = "Production",
                ["Encryption:MasterKey"] = "12345678901234567890123456789012",
            },
            Environments.Production);

        var exception = Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptions<ElasticApmSettings>>().Value);

        Assert.Contains("ElasticApm:ServerUrl is required.", exception.Failures);
    }


    [Fact]
    public void EncryptionMasterKey_RequiredInProduction()
    {
        using var serviceProvider = BuildServiceProvider(
            new Dictionary<string, string?>
            {
                ["MongoDb:ConnectionString"] = "mongodb://localhost:27017",
                ["MongoDb:DatabaseName"] = "hookbridge",
                ["Kafka:BootstrapServers"] = "localhost:9092",
                ["Kafka:MessageTimeoutMs"] = "10000",
                ["Jwt:Issuer"] = "issuer",
                ["Jwt:Audience"] = "audience",
                ["Jwt:Secret"] = "12345678901234567890123456789012",
                ["Jwt:ExpiryMinutes"] = "60",
                ["Stripe:SecretKey"] = "sk_live_test",
                ["Stripe:WebhookSecret"] = "whsec_test",
                ["Stripe:StarterPriceId"] = "price_starter",
                ["Stripe:ProPriceId"] = "price_pro",
                ["Stripe:SuccessUrl"] = "https://hookbridge.app/success",
                ["Stripe:CancelUrl"] = "https://hookbridge.app/cancel",
                ["Elastic:ServiceName"] = "hookbridge-worker",
                ["Elastic:Environment"] = "Production",
            },
            Environments.Production);

        var exception = Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptions<EncryptionSettings>>().Value);

        Assert.Contains("Encryption:MasterKey is required.", exception.Failures);
    }

    [Fact]
    public void EncryptionMasterKey_MinLength32InProduction()
    {
        using var serviceProvider = BuildServiceProvider(
            new Dictionary<string, string?>
            {
                ["MongoDb:ConnectionString"] = "mongodb://localhost:27017",
                ["MongoDb:DatabaseName"] = "hookbridge",
                ["Kafka:BootstrapServers"] = "localhost:9092",
                ["Kafka:MessageTimeoutMs"] = "10000",
                ["Jwt:Issuer"] = "issuer",
                ["Jwt:Audience"] = "audience",
                ["Jwt:Secret"] = "12345678901234567890123456789012",
                ["Jwt:ExpiryMinutes"] = "60",
                ["Stripe:SecretKey"] = "sk_live_test",
                ["Stripe:WebhookSecret"] = "whsec_test",
                ["Stripe:StarterPriceId"] = "price_starter",
                ["Stripe:ProPriceId"] = "price_pro",
                ["Stripe:SuccessUrl"] = "https://hookbridge.app/success",
                ["Stripe:CancelUrl"] = "https://hookbridge.app/cancel",
                ["Elastic:ServiceName"] = "hookbridge-worker",
                ["Elastic:Environment"] = "Production",
                ["Encryption:MasterKey"] = "short-master-key",
            },
            Environments.Production);

        var exception = Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptions<EncryptionSettings>>().Value);

        Assert.Contains("Encryption:MasterKey must be at least 32 characters long.", exception.Failures);
    }

    [Fact]
    public void ValidConfiguration_PassesValidation()
    {
        using var serviceProvider = BuildServiceProvider(
            new Dictionary<string, string?>
            {
                ["MongoDb:ConnectionString"] = "mongodb://localhost:27017",
                ["MongoDb:DatabaseName"] = "hookbridge",
                ["Kafka:BootstrapServers"] = "localhost:9092",
                ["Kafka:ConsumerGroupId"] = "hookbridge-worker",
                ["Kafka:MessageTimeoutMs"] = "10000",
                ["Jwt:Issuer"] = "hookbridge",
                ["Jwt:Audience"] = "hookbridge-clients",
                ["Jwt:Secret"] = "12345678901234567890123456789012",
                ["Jwt:ExpiryMinutes"] = "60",
                ["Stripe:SecretKey"] = "sk_live_test",
                ["Stripe:WebhookSecret"] = "whsec_test",
                ["Stripe:StarterPriceId"] = "price_starter",
                ["Stripe:ProPriceId"] = "price_pro",
                ["Stripe:SuccessUrl"] = "https://hookbridge.app/success",
                ["Stripe:CancelUrl"] = "https://hookbridge.app/cancel",
                ["Elastic:EnableElasticsearchSink"] = "true",
                ["Elastic:ElasticsearchUrl"] = "http://localhost:9200",
                ["Elastic:ServiceName"] = "hookbridge-worker",
                ["Elastic:Environment"] = "Production",
                ["ElasticApm:Enabled"] = "true",
                ["ElasticApm:ServerUrl"] = "http://localhost:8200",
                ["ElasticApm:ServiceName"] = "hookbridge-worker",
                ["ElasticApm:Environment"] = "Production",
                ["Encryption:MasterKey"] = "12345678901234567890123456789012",
            },
            Environments.Production,
            requireKafkaConsumerGroupId: true);

        _ = serviceProvider.GetRequiredService<IOptions<MongoDbSettings>>().Value;
        _ = serviceProvider.GetRequiredService<IOptions<KafkaSettings>>().Value;
        _ = serviceProvider.GetRequiredService<IOptions<JwtSettings>>().Value;
        _ = serviceProvider.GetRequiredService<IOptions<StripeSettings>>().Value;
        _ = serviceProvider.GetRequiredService<IOptions<ElasticSettings>>().Value;
        _ = serviceProvider.GetRequiredService<IOptions<ElasticApmSettings>>().Value;
        _ = serviceProvider.GetRequiredService<IOptions<EncryptionSettings>>().Value;
    }

    private static ServiceProvider BuildServiceProvider(
        IDictionary<string, string?> values,
        string environmentName,
        bool requireKafkaConsumerGroupId = false)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructureServices(
            configuration,
            new TestHostEnvironment(environmentName),
            requireKafkaConsumerGroupId);

        return services.BuildServiceProvider();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "HookBridge.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(AppContext.BaseDirectory);
    }
}
