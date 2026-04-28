using HookBridge.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class ProductionReadinessServiceTests
{
    [Fact]
    public async Task MissingMongoConfig_FailsReadiness()
    {
        var service = CreateService(
            [
                Pair("Kafka:BootstrapServers", "localhost:9092"),
                Pair("Jwt:Secret", Secret()),
                Pair("Encryption:MasterKey", Secret()),
                Pair("Stripe:SecretKey", "sk_test_x"),
                Pair("Stripe:WebhookSecret", "whsec_x"),
                Pair("RateLimit:Enabled", "true"),
                Pair("DemoData:Enabled", "false"),
                Pair("Cors:AllowedOrigins:0", "https://app.hookbridge.com"),
                Pair("ASPNETCORE_URLS", "https://+:443"),
                Pair("Hsts:Enabled", "true"),
            ],
            Environments.Production);

        var response = await service.CheckAsync();

        Assert.False(response.IsReady);
        var check = Assert.Single(response.Checks.Where(x => x.Name.Contains("MongoDB", StringComparison.OrdinalIgnoreCase)));
        Assert.False(check.IsReady);
    }

    [Fact]
    public async Task JwtSecretTooShort_FailsReadiness()
    {
        var service = CreateService(BuildValidProductionPairs().Append(Pair("Jwt:Secret", "short")), Environments.Production);

        var response = await service.CheckAsync();

        Assert.False(response.IsReady);
        var check = Assert.Single(response.Checks.Where(x => x.Name.Contains("JWT secret", StringComparison.OrdinalIgnoreCase)));
        Assert.False(check.IsReady);
    }

    [Fact]
    public async Task EncryptionKeyTooShort_FailsReadiness()
    {
        var service = CreateService(BuildValidProductionPairs().Append(Pair("Encryption:MasterKey", "too-short")), Environments.Production);

        var response = await service.CheckAsync();

        Assert.False(response.IsReady);
        var check = Assert.Single(response.Checks.Where(x => x.Name.Contains("Encryption", StringComparison.OrdinalIgnoreCase)));
        Assert.False(check.IsReady);
    }

    [Fact]
    public async Task StripeMissingInProduction_FailsReadiness()
    {
        var pairs = BuildValidProductionPairs()
            .Where(x => x.Key != "Stripe:SecretKey" && x.Key != "Stripe:WebhookSecret");
        var service = CreateService(pairs, Environments.Production);

        var response = await service.CheckAsync();

        Assert.False(response.IsReady);
        var check = Assert.Single(response.Checks.Where(x => x.Name.Contains("Stripe", StringComparison.OrdinalIgnoreCase)));
        Assert.False(check.IsReady);
    }

    [Fact]
    public async Task DemoDataEnabledInProduction_FailsReadiness()
    {
        var service = CreateService(BuildValidProductionPairs().Append(Pair("DemoData:Enabled", "true")), Environments.Production);

        var response = await service.CheckAsync();

        Assert.False(response.IsReady);
        var check = Assert.Single(response.Checks.Where(x => x.Name.Contains("Demo data", StringComparison.OrdinalIgnoreCase)));
        Assert.False(check.IsReady);
    }

    [Fact]
    public async Task ValidConfig_ReturnsReadyTrue()
    {
        var service = CreateService(BuildValidProductionPairs(), Environments.Production);

        var response = await service.CheckAsync();

        Assert.True(response.IsReady);
        Assert.All(response.Checks.Where(x => x.Name is "Elastic configured if enabled" or "Elastic APM configured if enabled" or "Email settings configured if enabled"), x => Assert.True(x.IsReady));
    }

    private static TestableProductionReadinessService CreateService(IEnumerable<KeyValuePair<string, string?>> pairs, string environment)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(pairs)
            .Build();

        return new TestableProductionReadinessService(
            config,
            new TestHostEnvironment(environment),
            NullLogger<ProductionReadinessService>.Instance);
    }

    private static IEnumerable<KeyValuePair<string, string?>> BuildValidProductionPairs()
    {
        yield return Pair("MongoDb:ConnectionString", "mongodb://localhost:27017");
        yield return Pair("MongoDb:DatabaseName", "hookbridge");
        yield return Pair("Kafka:BootstrapServers", "localhost:9092");
        yield return Pair("Jwt:Secret", Secret());
        yield return Pair("Encryption:MasterKey", Secret());
        yield return Pair("Stripe:SecretKey", "sk_live_x");
        yield return Pair("Stripe:WebhookSecret", "whsec_x");
        yield return Pair("RateLimit:Enabled", "true");
        yield return Pair("DemoData:Enabled", "false");
        yield return Pair("Cors:AllowedOrigins:0", "https://app.hookbridge.com");
        yield return Pair("ASPNETCORE_URLS", "https://+:443");
        yield return Pair("Hsts:Enabled", "true");
    }

    private static KeyValuePair<string, string?> Pair(string key, string? value) => new(key, value);

    private static string Secret() => new('x', 32);

    private sealed class TestableProductionReadinessService(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        Microsoft.Extensions.Logging.ILogger<ProductionReadinessService> logger)
        : ProductionReadinessService(configuration, hostEnvironment, logger)
    {
        protected override Task<bool> IsMongoReachableAsync(string connectionString, string databaseName, CancellationToken cancellationToken)
            => Task.FromResult(true);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "HookBridge.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
