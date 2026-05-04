using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using HookBridge.Api.Extensions;
using HookBridge.Api.Health;
using HookBridge.Api.Middleware;
using HookBridge.Infrastructure.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace HookBridge.Api.Tests;

public sealed class ObservabilityMiddlewareAndHealthTests
{
    [Fact]
    public async Task CorrelationId_IsGenerated_WhenMissing()
    {
        using var host = await BuildMiddlewareHostAsync();
        var client = host.CreateClient();

        var response = await client.GetAsync("/test");

        Assert.True(response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values));
        var correlationId = Assert.Single(values);
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
    }

    [Fact]
    public async Task CorrelationId_FromHeader_IsPreserved()
    {
        using var host = await BuildMiddlewareHostAsync();
        var client = host.CreateClient();
        const string expected = "corr-from-request-123";

        var request = new HttpRequestMessage(HttpMethod.Get, "/test");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, expected);

        var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values));
        Assert.Equal(expected, Assert.Single(values));
    }

    [Fact]
    public async Task Response_ContainsCorrelationId_Header()
    {
        using var host = await BuildMiddlewareHostAsync();
        var client = host.CreateClient();

        var response = await client.GetAsync("/test");

        Assert.True(response.Headers.Contains(CorrelationIdMiddleware.HeaderName));
    }

    [Fact]
    public async Task RequestLogging_DoesNotIncludeAuthorizationHeader()
    {
        var sink = new InMemoryLogSink();
        using var host = await BuildMiddlewareHostAsync(sink);
        var client = host.CreateClient();
        const string secret = "Bearer super-secret-token";

        var request = new HttpRequestMessage(HttpMethod.Get, "/test");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "super-secret-token");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(sink.Messages, message => message.Contains(secret, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ElasticSettings_Binding_Works()
    {
        var values = new Dictionary<string, string?>
        {
            ["Elastic:ElasticsearchUrl"] = "http://localhost:9200",
            ["Elastic:Environment"] = "Development",
            ["Elastic:ServiceName"] = "hookbridge-api",
            ["Elastic:EnableElasticsearchSink"] = "true",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.Configure<ElasticSettings>(configuration.GetSection("Elastic"));

        using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IOptions<ElasticSettings>>().Value;

        Assert.Equal("http://localhost:9200", settings.ElasticsearchUrl);
        Assert.Equal("Development", settings.Environment);
        Assert.Equal("hookbridge-api", settings.ServiceName);
        Assert.True(settings.EnableElasticsearchSink);
    }


    [Fact]
    public void ElasticApmSettings_Binding_Works()
    {
        var values = new Dictionary<string, string?>
        {
            ["ElasticApm:ServerUrl"] = "http://localhost:8200",
            ["ElasticApm:ServiceName"] = "hookbridge-api",
            ["ElasticApm:Environment"] = "Development",
            ["ElasticApm:Enabled"] = "true",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.Configure<ElasticApmSettings>(configuration.GetSection("ElasticApm"));

        using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IOptions<ElasticApmSettings>>().Value;

        Assert.Equal("http://localhost:8200", settings.ServerUrl);
        Assert.Equal("hookbridge-api", settings.ServiceName);
        Assert.Equal("Development", settings.Environment);
        Assert.True(settings.Enabled);
    }

    [Fact]
    public async Task ElasticApmHealthEndpoint_ReturnsHealthy_WhenEnabled()
    {
        using var host = await BuildHealthHostAsync("http://127.0.0.1:1", apmEnabled: true);
        var client = host.CreateClient();

        var response = await client.GetFromJsonAsync<ApmHealthResponse>("/api/v1/health/apm");

        Assert.NotNull(response);
        Assert.Equal("ElasticAPM", response.Service);
        Assert.True(response.IsHealthy);
        Assert.Equal("Elastic APM is enabled.", response.Message);
    }

    [Fact]
    public async Task ElasticApmHealthEndpoint_ReturnsUnhealthy_WhenDisabled()
    {
        using var host = await BuildHealthHostAsync("http://127.0.0.1:1", apmEnabled: false);
        var client = host.CreateClient();

        var response = await client.GetFromJsonAsync<ApmHealthResponse>("/api/v1/health/apm");

        Assert.NotNull(response);
        Assert.Equal("ElasticAPM", response.Service);
        Assert.False(response.IsHealthy);
        Assert.Equal("Elastic APM is disabled.", response.Message);
    }

    [Fact]
    public async Task ElasticsearchHealthEndpoint_ReturnsUnhealthy_WhenUnavailable()
    {
        using var host = await BuildHealthHostAsync("http://127.0.0.1:1");
        var client = host.CreateClient();

        var response = await client.GetFromJsonAsync<ElasticsearchHealthResponse>("/api/v1/health/elasticsearch");

        Assert.NotNull(response);
        Assert.False(response.IsHealthy);
        Assert.StartsWith("Elasticsearch connection failed.", response.Message);
    }

    [Fact]
    public async Task WorkerHealthEndpoint_ReturnsHealthy()
    {
        using var host = await BuildHealthHostAsync("http://127.0.0.1:1");
        var client = host.CreateClient();

        var response = await client.GetFromJsonAsync<WorkerHealthResponse>("/api/v1/health/worker");

        Assert.NotNull(response);
        Assert.Equal("Worker", response.Service);
        Assert.True(response.IsHealthy);
        Assert.Equal("Worker health endpoint is reachable.", response.Message);
    }

    private static async Task<TestServer> BuildMiddlewareHostAsync(InMemoryLogSink? sink = null)
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging(logging =>
                {
                    if (sink is not null)
                    {
                        logging.ClearProviders();
                        logging.AddProvider(new InMemoryLoggerProvider(sink));
                    }
                });
            })
            .Configure(app =>
            {
                app.UseMiddleware<CorrelationIdMiddleware>();
                app.UseMiddleware<RequestLoggingMiddleware>();
                app.Run(async context => await context.Response.WriteAsync("ok"));
            });

        var server = new TestServer(builder);
        await Task.CompletedTask;
        return server;
    }

    private static async Task<TestServer> BuildHealthHostAsync(string elasticsearchUrl, bool apmEnabled = false)
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddHttpClient();
                services.Configure<ElasticSettings>(options =>
                {
                    options.ElasticsearchUrl = elasticsearchUrl;
                    options.Environment = "Development";
                    options.ServiceName = "hookbridge-api";
                    options.EnableElasticsearchSink = false;
                });
                services.Configure<ElasticApmSettings>(options =>
                {
                    options.ServerUrl = "http://localhost:8200";
                    options.ServiceName = "hookbridge-api";
                    options.Environment = "Development";
                    options.Enabled = apmEnabled;
                });
                services.AddScoped<IElasticsearchHealthService, ElasticsearchHealthService>();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapHookBridgeHealthEndpoints());
            });

        var server = new TestServer(builder);
        await Task.CompletedTask;
        return server;
    }

    private sealed class InMemoryLogSink
    {
        public ConcurrentBag<string> Messages { get; } = [];
    }

    private sealed class InMemoryLoggerProvider(InMemoryLogSink sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new InMemoryLogger(sink);

        public void Dispose()
        {
        }
    }

    private sealed class InMemoryLogger(InMemoryLogSink sink) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            sink.Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed class ApmHealthResponse
    {
        public string Service { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    private sealed record WorkerHealthResponse(string Service, bool IsHealthy, string Message);
}
