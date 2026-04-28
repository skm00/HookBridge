using HookBridge.Application.DTOs.System;
using HookBridge.Application.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace HookBridge.Application.Services;

public class ProductionReadinessService(
    IConfiguration configuration,
    IHostEnvironment hostEnvironment,
    ILogger<ProductionReadinessService> logger) : IProductionReadinessService
{
    public async Task<ProductionReadinessResponseDto> CheckAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<(ProductionReadinessItemDto Item, bool IsCritical)>();
        var isProduction = hostEnvironment.IsProduction();

        checks.Add(await CheckMongoAsync(cancellationToken));
        checks.Add(CheckKafka());
        checks.Add(CheckJwtSecret());
        checks.Add(CheckEncryptionKey());
        checks.Add(CheckStripe(isProduction));
        checks.Add(CheckCors(isProduction));
        checks.Add(CheckRateLimiting());
        checks.Add(CheckElastic());
        checks.Add(CheckElasticApm());
        checks.Add(CheckHttpsAndHsts(isProduction));
        checks.Add(CheckEmail());
        checks.Add(CheckDemoData(isProduction));

        var criticalFailureCount = checks.Count(x => x.IsCritical && !x.Item.IsReady);
        if (criticalFailureCount > 0)
        {
            logger.LogWarning("Production readiness check completed with {CriticalFailureCount} critical failure(s).", criticalFailureCount);
        }

        return new ProductionReadinessResponseDto
        {
            IsReady = criticalFailureCount == 0,
            Checks = checks.Select(x => x.Item).ToList(),
        };
    }

    protected virtual async Task<bool> IsMongoReachableAsync(string connectionString, string databaseName, CancellationToken cancellationToken)
    {
        var client = new MongoClient(connectionString);
        var database = client.GetDatabase(databaseName);
        await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
        return true;
    }

    private async Task<(ProductionReadinessItemDto, bool)> CheckMongoAsync(CancellationToken cancellationToken)
    {
        const string name = "MongoDB connection configured and reachable";
        var connectionString = configuration["MongoDb:ConnectionString"];
        var databaseName = configuration["MongoDb:DatabaseName"];
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(databaseName))
        {
            return (Fail(name, "MongoDB configuration is missing ConnectionString or DatabaseName."), true);
        }

        try
        {
            var reachable = await IsMongoReachableAsync(connectionString, databaseName, cancellationToken);
            return reachable
                ? (Pass(name, "MongoDB is configured and reachable."), true)
                : (Fail(name, "MongoDB is configured but not reachable."), true);
        }
        catch (Exception)
        {
            return (Fail(name, "MongoDB is configured but not reachable."), true);
        }
    }

    private (ProductionReadinessItemDto, bool) CheckKafka()
    {
        const string name = "Kafka configured (BootstrapServers present)";
        var bootstrapServers = configuration["Kafka:BootstrapServers"];
        return string.IsNullOrWhiteSpace(bootstrapServers)
            ? (Fail(name, "Kafka BootstrapServers is missing."), true)
            : (Pass(name, "Kafka BootstrapServers is configured."), true);
    }

    private (ProductionReadinessItemDto, bool) CheckJwtSecret()
    {
        const string name = "JWT secret valid (>= 32 chars)";
        var secret = configuration["Jwt:Secret"];
        return secret is { Length: >= 32 }
            ? (Pass(name, "JWT secret length is valid."), true)
            : (Fail(name, "JWT secret is missing or shorter than 32 characters."), true);
    }

    private (ProductionReadinessItemDto, bool) CheckEncryptionKey()
    {
        const string name = "Encryption MasterKey valid (>= 32 chars)";
        var key = configuration["Encryption:MasterKey"];
        return key is { Length: >= 32 }
            ? (Pass(name, "Encryption master key length is valid."), true)
            : (Fail(name, "Encryption master key is missing or shorter than 32 characters."), true);
    }

    private (ProductionReadinessItemDto, bool) CheckStripe(bool isProduction)
    {
        const string name = "Stripe configured (SecretKey + WebhookSecret)";
        var hasSecretKey = !string.IsNullOrWhiteSpace(configuration["Stripe:SecretKey"]);
        var hasWebhookSecret = !string.IsNullOrWhiteSpace(configuration["Stripe:WebhookSecret"]);
        if (hasSecretKey && hasWebhookSecret)
        {
            return (Pass(name, "Stripe credentials are configured."), true);
        }

        if (!isProduction)
        {
            return (Pass(name, "Stripe credentials are missing, but this is allowed outside Production."), false);
        }

        return (Fail(name, "Stripe SecretKey and WebhookSecret are required in Production."), true);
    }

    private (ProductionReadinessItemDto, bool) CheckCors(bool isProduction)
    {
        const string name = "CORS configured (no wildcard in Production)";
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        if (origins.Length == 0)
        {
            return isProduction
                ? (Fail(name, "CORS allowed origins are not configured for Production."), true)
                : (Pass(name, "CORS allowed origins are not configured (acceptable outside Production)."), false);
        }

        var hasWildcard = origins.Any(origin => origin.Contains('*'));
        if (isProduction && hasWildcard)
        {
            return (Fail(name, "CORS wildcard origins are not allowed in Production."), true);
        }

        return (Pass(name, "CORS allowed origins are configured."), true);
    }

    private (ProductionReadinessItemDto, bool) CheckRateLimiting()
    {
        const string name = "Rate limiting enabled";
        return configuration.GetValue("RateLimit:Enabled", true)
            ? (Pass(name, "Rate limiting is enabled."), true)
            : (Fail(name, "Rate limiting is disabled."), true);
    }

    private (ProductionReadinessItemDto, bool) CheckElastic()
    {
        const string name = "Elastic configured if enabled";
        var enabled = configuration.GetValue("Elastic:EnableElasticsearchSink", false);
        if (!enabled)
        {
            return (Pass(name, "Elastic sink is disabled."), false);
        }

        var elasticUrl = configuration["Elastic:ElasticsearchUrl"];
        return string.IsNullOrWhiteSpace(elasticUrl)
            ? (Fail(name, "Elastic sink is enabled, but ElasticsearchUrl is missing."), false)
            : (Pass(name, "Elastic sink is enabled and configured."), false);
    }

    private (ProductionReadinessItemDto, bool) CheckElasticApm()
    {
        const string name = "Elastic APM configured if enabled";
        var enabled = configuration.GetValue("ElasticApm:Enabled", false);
        if (!enabled)
        {
            return (Pass(name, "Elastic APM is disabled."), false);
        }

        var hasServerUrl = !string.IsNullOrWhiteSpace(configuration["ElasticApm:ServerUrl"]);
        var hasServiceName = !string.IsNullOrWhiteSpace(configuration["ElasticApm:ServiceName"]);
        var hasEnvironment = !string.IsNullOrWhiteSpace(configuration["ElasticApm:Environment"]);

        return hasServerUrl && hasServiceName && hasEnvironment
            ? (Pass(name, "Elastic APM is enabled and configured."), false)
            : (Fail(name, "Elastic APM is enabled, but required settings are missing."), false);
    }

    private (ProductionReadinessItemDto, bool) CheckHttpsAndHsts(bool isProduction)
    {
        const string name = "HTTPS/HSTS enabled in Production";
        if (!isProduction)
        {
            return (Pass(name, "HTTPS/HSTS production requirement is skipped outside Production."), false);
        }

        var urls = configuration["ASPNETCORE_URLS"] ?? configuration["urls"] ?? string.Empty;
        var usesHttps = urls.Contains("https://", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(configuration["Kestrel:Endpoints:Https:Url"])
            || configuration.GetValue<bool>("Security:RequireHttps");
        var hstsEnabled = configuration.GetValue<bool?>("Hsts:Enabled") ?? configuration.GetValue<bool>("Security:EnableHsts");

        return usesHttps && hstsEnabled
            ? (Pass(name, "HTTPS and HSTS are configured for Production."), true)
            : (Fail(name, "Production requires HTTPS and HSTS to be enabled."), true);
    }

    private (ProductionReadinessItemDto, bool) CheckEmail()
    {
        const string name = "Email settings configured if enabled";
        var enabled = configuration.GetValue("Email:Enabled", false);
        if (!enabled)
        {
            return (Pass(name, "Email notifications are disabled."), false);
        }

        var provider = configuration["Email:Provider"];
        var smtpHost = configuration["Email:SmtpHost"];
        var smtpPort = configuration.GetValue("Email:SmtpPort", 0);
        var fromEmail = configuration["Email:FromEmail"];

        var valid = !string.IsNullOrWhiteSpace(provider)
            && !string.IsNullOrWhiteSpace(smtpHost)
            && smtpPort > 0
            && !string.IsNullOrWhiteSpace(fromEmail);

        return valid
            ? (Pass(name, "Email settings are enabled and configured."), false)
            : (Fail(name, "Email is enabled, but provider/SMTP/from-email settings are incomplete."), false);
    }

    private (ProductionReadinessItemDto, bool) CheckDemoData(bool isProduction)
    {
        const string name = "Demo data disabled in Production";
        var enabled = configuration.GetValue("DemoData:Enabled", false);

        if (!enabled)
        {
            return (Pass(name, "Demo data seeding is disabled."), true);
        }

        return isProduction
            ? (Fail(name, "Demo data seeding must be disabled in Production."), true)
            : (Pass(name, "Demo data seeding is enabled outside Production."), false);
    }

    private static ProductionReadinessItemDto Pass(string name, string message) =>
        new() { Name = name, IsReady = true, Message = message };

    private static ProductionReadinessItemDto Fail(string name, string message) =>
        new() { Name = name, IsReady = false, Message = message };
}
