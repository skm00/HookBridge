using System.Net;
using FluentAssertions;
using HookBridge.Api.Health;
using HookBridge.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace HookBridge.Api.Tests;

public sealed class ElasticsearchHealthServiceTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenUrlIsInvalid_ReturnsUnhealthyWithoutCallingHttpClient()
    {
        var factory = new Mock<IHttpClientFactory>();
        var service = CreateService("not-a-valid-url", factory.Object);

        var result = await service.CheckHealthAsync(CancellationToken.None);

        result.IsHealthy.Should().BeFalse();
        result.Message.Should().Be("Elasticsearch connection failed. Reason: Invalid Elasticsearch URL.");
        factory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenClusterHealthSucceeds_ReturnsHealthy()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var service = CreateService("http://elastic:9200", BuildFactory(handler));

        var result = await service.CheckHealthAsync(CancellationToken.None);

        result.IsHealthy.Should().BeTrue();
        result.Message.Should().Be("Elasticsearch connection is healthy.");
        handler.Requests.Should().ContainSingle(request => request.RequestUri!.ToString() == "http://elastic:9200/_cluster/health");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenClusterHealthReturnsError_ReturnsUnhealthy()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            ReasonPhrase = "Service Unavailable",
        });
        var service = CreateService("http://elastic:9200", BuildFactory(handler));

        var result = await service.CheckHealthAsync(CancellationToken.None);

        result.IsHealthy.Should().BeFalse();
        result.Message.Should().Be("Elasticsearch connection failed. Reason: HTTP 503 Service Unavailable.");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenHttpClientThrows_ReturnsUnhealthyWithReason()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var service = CreateService("http://elastic:9200", BuildFactory(handler));

        var result = await service.CheckHealthAsync(CancellationToken.None);

        result.IsHealthy.Should().BeFalse();
        result.Message.Should().Be("Elasticsearch connection failed. Reason: connection refused");
    }

    private static ElasticsearchHealthService CreateService(string url, IHttpClientFactory httpClientFactory)
    {
        return new ElasticsearchHealthService(
            httpClientFactory,
            Options.Create(new ElasticSettings { ElasticsearchUrl = url }));
    }

    private static IHttpClientFactory BuildFactory(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(x => x.CreateClient(nameof(ElasticsearchHealthService)))
            .Returns(new HttpClient(handler));

        return factory.Object;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responseFactory(request));
        }
    }
}
