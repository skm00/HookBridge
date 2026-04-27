using HookBridge.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace HookBridge.Api.Health;

public interface IElasticsearchHealthService
{
    Task<ElasticsearchHealthResponse> CheckHealthAsync(CancellationToken cancellationToken);
}

public sealed class ElasticsearchHealthService(IHttpClientFactory httpClientFactory, IOptions<ElasticSettings> elasticOptions) : IElasticsearchHealthService
{
    public async Task<ElasticsearchHealthResponse> CheckHealthAsync(CancellationToken cancellationToken)
    {
        var settings = elasticOptions.Value;

        try
        {
            if (!Uri.TryCreate(settings.ElasticsearchUrl, UriKind.Absolute, out var uri))
            {
                return new ElasticsearchHealthResponse
                {
                    IsHealthy = false,
                    Message = "Elasticsearch connection failed. Reason: Invalid Elasticsearch URL.",
                };
            }

            var client = httpClientFactory.CreateClient(nameof(ElasticsearchHealthService));
            client.Timeout = TimeSpan.FromSeconds(2);

            using var response = await client.GetAsync(new Uri(uri, "/_cluster/health"), cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new ElasticsearchHealthResponse
                {
                    IsHealthy = false,
                    Message = $"Elasticsearch connection failed. Reason: HTTP {(int)response.StatusCode} {response.ReasonPhrase}.",
                };
            }

            return new ElasticsearchHealthResponse
            {
                IsHealthy = true,
                Message = "Elasticsearch connection is healthy.",
            };
        }
        catch (Exception ex)
        {
            return new ElasticsearchHealthResponse
            {
                IsHealthy = false,
                Message = $"Elasticsearch connection failed. Reason: {ex.Message}",
            };
        }
    }
}
