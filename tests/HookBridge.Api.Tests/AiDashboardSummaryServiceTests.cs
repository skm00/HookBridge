using HookBridge.AI.Worker.Mongo;
using HookBridge.Api.Configuration;
using HookBridge.Api.Controllers;
using HookBridge.Api.Services.AiDashboard;
using HookBridge.Application.DTOs.AiDashboard;
using HookBridge.Application.Interfaces;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.Api.Tests;

public sealed class AiDashboardSummaryServiceTests
{
    private static readonly DateTime NowUtc = new(2026, 5, 14, 10, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Summary_ReturnsTotalsCorrectly()
    {
        var service = CreateService();

        var result = await service.GetSummaryAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(10, result.TotalAiAnalyses);
        Assert.Equal(4, result.TotalAnomalies);
        Assert.Equal(2, result.TotalSecurityFindings);
        Assert.Equal(3, result.TotalHighRiskEndpoints);
        Assert.Equal(7, result.TotalRetryRecommendations);
        Assert.Equal(1, result.TotalDeadLetterRecommendations);
    }

    [Fact]
    public async Task DefaultDateRange_UsesLast24Hours()
    {
        var analysisRepository = new FakeAiAnalysisResultRepository();
        var service = CreateService(analysisRepository: analysisRepository);

        var result = await service.GetSummaryAsync(new AiDashboardSummaryRequestDto(), CancellationToken.None);

        Assert.Equal(NowUtc.AddHours(-24), result.FromUtc);
        Assert.Equal(NowUtc, result.ToUtc);
        Assert.Equal(result.FromUtc, analysisRepository.LastFilter!.FromUtc);
        Assert.Equal(result.ToUtc, analysisRepository.LastFilter.ToUtc);
    }

    [Fact]
    public async Task RiskDistribution_IsCalculatedAcrossRepositories()
    {
        var result = await CreateService().GetSummaryAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(1, result.RiskDistribution.Unknown);
        Assert.Equal(2, result.RiskDistribution.Low);
        Assert.Equal(3, result.RiskDistribution.Medium);
        Assert.Equal(4, result.RiskDistribution.High);
        Assert.Equal(5, result.RiskDistribution.Critical);
    }

    [Fact]
    public async Task AnomalyTypeDistribution_IsCalculated()
    {
        var result = await CreateService().GetSummaryAsync(CreateRequest(), CancellationToken.None);

        var item = Assert.Single(result.AnomalyTypeDistribution.Where(x => x.Name == "RateLimitSpike"));
        Assert.Equal(3, item.Count);
        Assert.Equal(75, item.Percentage);
    }

    [Fact]
    public async Task RetryActionDistribution_IsCalculated()
    {
        var result = await CreateService().GetSummaryAsync(CreateRequest(), CancellationToken.None);

        var item = Assert.Single(result.RetryActionDistribution.Where(x => x.Name == "RetryWithBackoff"));
        Assert.Equal(7, item.Count);
        Assert.Equal(87.5, item.Percentage);
    }

    [Fact]
    public async Task EndpointHealthDistribution_IsCalculated()
    {
        var result = await CreateService().GetSummaryAsync(CreateRequest(), CancellationToken.None);

        var item = Assert.Single(result.EndpointHealthDistribution.Where(x => x.Name == "Healthy"));
        Assert.Equal(6, item.Count);
        Assert.Equal(60, item.Percentage);
    }

    [Fact]
    public async Task AverageConfidenceScore_IsCalculated()
    {
        var result = await CreateService().GetSummaryAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(0.8, result.AverageConfidenceScore);
    }

    [Fact]
    public async Task RecentFindings_AreMappedAndLimited()
    {
        var service = CreateService(options: new AiDashboardOptions { RecentFindingsLimit = 2 });

        var result = await service.GetSummaryAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(2, result.RecentFindings.Count);
        Assert.Equal("Security", result.RecentFindings[0].FindingType);
        Assert.Equal("Anomaly", result.RecentFindings[1].FindingType);
    }

    [Fact]
    public async Task EnvironmentFilter_IsPassedToRepositories()
    {
        var analysisRepository = new FakeAiAnalysisResultRepository();
        var service = CreateService(analysisRepository: analysisRepository);

        await service.GetSummaryAsync(CreateRequest(environment: "qa"), CancellationToken.None);

        Assert.Equal("qa", analysisRepository.LastFilter!.Environment);
    }

    [Fact]
    public async Task CustomerIdFilter_IsPassedToRepositories()
    {
        var analysisRepository = new FakeAiAnalysisResultRepository();
        var service = CreateService(analysisRepository: analysisRepository);

        await service.GetSummaryAsync(CreateRequest(customerId: "cust_123"), CancellationToken.None);

        Assert.Equal("cust_123", analysisRepository.LastFilter!.CustomerId);
    }

    [Fact]
    public async Task SubscriptionIdFilter_IsPassedToRepositories()
    {
        var analysisRepository = new FakeAiAnalysisResultRepository();
        var service = CreateService(analysisRepository: analysisRepository);

        await service.GetSummaryAsync(CreateRequest(subscriptionId: "sub_456"), CancellationToken.None);

        Assert.Equal("sub_456", analysisRepository.LastFilter!.SubscriptionId);
    }

    [Fact]
    public async Task EndpointIdFilter_IsPassedToRepositories()
    {
        var analysisRepository = new FakeAiAnalysisResultRepository();
        var service = CreateService(analysisRepository: analysisRepository);

        await service.GetSummaryAsync(CreateRequest(endpointId: "endpoint_789"), CancellationToken.None);

        Assert.Equal("endpoint_789", analysisRepository.LastFilter!.EndpointId);
    }

    [Fact]
    public async Task InvalidDateRange_ThrowsValidationError()
    {
        var service = CreateService();
        var request = CreateRequest(fromUtc: NowUtc, toUtc: NowUtc.AddMinutes(-1));

        var exception = await Assert.ThrowsAsync<AiDashboardValidationException>(() => service.GetSummaryAsync(request, CancellationToken.None));

        Assert.Equal(nameof(AiDashboardSummaryRequestDto.ToUtc), exception.FieldName);
    }

    [Fact]
    public async Task DateRangeOverMaxLimit_ThrowsValidationError()
    {
        var service = CreateService(options: new AiDashboardOptions { MaxLookbackDays = 1 });
        var request = CreateRequest(fromUtc: NowUtc.AddDays(-2), toUtc: NowUtc);

        var exception = await Assert.ThrowsAsync<AiDashboardValidationException>(() => service.GetSummaryAsync(request, CancellationToken.None));

        Assert.Equal(nameof(AiDashboardSummaryRequestDto.FromUtc), exception.FieldName);
    }


    [Fact]
    public async Task NonUtcFromUtc_ThrowsValidationError()
    {
        var service = CreateService();
        var request = CreateRequest(
            fromUtc: new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Local),
            toUtc: new DateTime(2026, 5, 14, 23, 59, 59, DateTimeKind.Utc));

        var exception = await Assert.ThrowsAsync<AiDashboardValidationException>(() => service.GetSummaryAsync(request, CancellationToken.None));

        Assert.Equal(nameof(AiDashboardSummaryRequestDto.FromUtc), exception.FieldName);
    }

    [Fact]
    public async Task NonUtcToUtc_ThrowsValidationError()
    {
        var service = CreateService();
        var request = CreateRequest(
            fromUtc: new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc),
            toUtc: new DateTime(2026, 5, 14, 23, 59, 59, DateTimeKind.Local));

        var exception = await Assert.ThrowsAsync<AiDashboardValidationException>(() => service.GetSummaryAsync(request, CancellationToken.None));

        Assert.Equal(nameof(AiDashboardSummaryRequestDto.ToUtc), exception.FieldName);
    }

    [Fact]
    public async Task ProvidedToUtcWithoutFromUtc_UsesLookbackFromProvidedToUtc()
    {
        var analysisRepository = new FakeAiAnalysisResultRepository();
        var service = CreateService(analysisRepository: analysisRepository, options: new AiDashboardOptions { DefaultLookbackHours = 6 });
        var toUtc = new DateTime(2026, 5, 14, 8, 0, 0, DateTimeKind.Utc);

        var result = await service.GetSummaryAsync(new AiDashboardSummaryRequestDto { ToUtc = toUtc }, CancellationToken.None);

        Assert.Equal(toUtc.AddHours(-6), result.FromUtc);
        Assert.Equal(toUtc, result.ToUtc);
        Assert.Equal(toUtc.AddHours(-6), analysisRepository.LastFilter!.FromUtc);
    }

    [Fact]
    public async Task EmptyRepositoryDistributions_ReturnZeroPercentagesAndUnknownRisk()
    {
        var analysisRepository = new FakeAiAnalysisResultRepository
        {
            TotalCount = 0,
            RiskCounts = new Dictionary<string, long> { ["UnexpectedRisk"] = 2 },
            RetryActionCounts = new Dictionary<string, long>(),
            AverageConfidenceScore = 0,
            RecentFindings = []
        };
        var anomalyRepository = new FakeAiAnomalyRecordRepository
        {
            TotalCount = 0,
            RiskCounts = new Dictionary<string, long>(),
            AnomalyTypeCounts = new Dictionary<string, long>(),
            RecentFindings = []
        };
        var securityRepository = new FakeAiSecurityAnalysisRepository
        {
            TotalCount = 0,
            RiskCounts = new Dictionary<string, long>(),
            AverageConfidenceScore = 0,
            RecentFindings = []
        };
        var riskRepository = new FakeCustomerEndpointRiskScoreRepository
        {
            HighRiskEndpointCount = 0,
            HealthStatusCounts = new Dictionary<string, long>()
        };
        var service = CreateService(
            analysisRepository: analysisRepository,
            anomalyRepository: anomalyRepository,
            securityRepository: securityRepository,
            riskRepository: riskRepository);

        var result = await service.GetSummaryAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(2, result.RiskDistribution.Unknown);
        Assert.Empty(result.AnomalyTypeDistribution);
        Assert.Empty(result.RetryActionDistribution);
        Assert.Empty(result.EndpointHealthDistribution);
        Assert.Equal(0, result.AverageConfidenceScore);
        Assert.Empty(result.RecentFindings);
    }

    [Fact]
    public async Task RetryImmediately_IsCountedAsRetryRecommendation()
    {
        var analysisRepository = new FakeAiAnalysisResultRepository
        {
            RetryActionCounts = new Dictionary<string, long>
            {
                ["RetryImmediately"] = 2,
                ["RetryWithBackoff"] = 3,
                ["PauseEndpoint"] = 4,
                ["MoveToDeadLetter"] = 5
            }
        };
        var service = CreateService(analysisRepository: analysisRepository);

        var result = await service.GetSummaryAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(5, result.TotalRetryRecommendations);
        Assert.Equal(5, result.TotalDeadLetterRecommendations);
    }

    [Fact]
    public async Task RepositoryFailure_IsLoggedAndRethrown()
    {
        var analysisRepository = new FakeAiAnalysisResultRepository { ThrowOnCount = true };
        var service = CreateService(analysisRepository: analysisRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetSummaryAsync(CreateRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task Controller_ReturnsOk()
    {
        var controller = new AiDashboardController(CreateService(), NullLogger<AiDashboardController>.Instance);

        var result = await controller.GetSummaryAsync(CreateRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<AiDashboardSummaryResponseDto>>(ok.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task Controller_ReturnsBadRequest()
    {
        var controller = new AiDashboardController(CreateService(), NullLogger<AiDashboardController>.Instance);

        var result = await controller.GetSummaryAsync(CreateRequest(fromUtc: NowUtc, toUtc: NowUtc), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Controller_ReturnsInternalServerError_OnUnexpectedException()
    {
        var controller = new AiDashboardController(new ThrowingDashboardSummaryService(), NullLogger<AiDashboardController>.Instance);

        var result = await controller.GetSummaryAsync(CreateRequest(), CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, error.StatusCode);
    }

    [Fact]
    public void RequiredServices_AreRegisteredInDi()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDateTimeProvider>(new FixedDateTimeProvider(NowUtc));
        services.AddSingleton<IAiAnalysisResultRepository, FakeAiAnalysisResultRepository>();
        services.AddSingleton<IAiAnomalyRecordRepository, FakeAiAnomalyRecordRepository>();
        services.AddSingleton<IAiSecurityAnalysisRepository, FakeAiSecurityAnalysisRepository>();
        services.AddSingleton<ICustomerEndpointRiskScoreRepository, FakeCustomerEndpointRiskScoreRepository>();
        services.AddSingleton(Options.Create(new AiDashboardOptions()));
        services.AddLogging();
        services.AddScoped<IAiDashboardSummaryService, AiDashboardSummaryService>();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IAiDashboardSummaryService>());
    }

    private static AiDashboardSummaryRequestDto CreateRequest(
        string? environment = null,
        string? customerId = null,
        string? subscriptionId = null,
        string? endpointId = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null)
        => new()
        {
            Environment = environment,
            CustomerId = customerId,
            SubscriptionId = subscriptionId,
            EndpointId = endpointId,
            FromUtc = fromUtc ?? new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = toUtc ?? new DateTime(2026, 5, 14, 23, 59, 59, DateTimeKind.Utc)
        };

    private static AiDashboardSummaryService CreateService(
        FakeAiAnalysisResultRepository? analysisRepository = null,
        FakeAiAnomalyRecordRepository? anomalyRepository = null,
        FakeAiSecurityAnalysisRepository? securityRepository = null,
        FakeCustomerEndpointRiskScoreRepository? riskRepository = null,
        AiDashboardOptions? options = null)
        => new(
            analysisRepository ?? new FakeAiAnalysisResultRepository(),
            anomalyRepository ?? new FakeAiAnomalyRecordRepository(),
            securityRepository ?? new FakeAiSecurityAnalysisRepository(),
            riskRepository ?? new FakeCustomerEndpointRiskScoreRepository(),
            new FixedDateTimeProvider(NowUtc),
            Options.Create(options ?? new AiDashboardOptions()),
            NullLogger<AiDashboardSummaryService>.Instance);

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class ThrowingDashboardSummaryService : IAiDashboardSummaryService
    {
        public Task<AiDashboardSummaryResponseDto> GetSummaryAsync(AiDashboardSummaryRequestDto request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unexpected failure.");
    }

    private sealed class FakeAiAnalysisResultRepository : IAiAnalysisResultRepository
    {
        public AiDashboardQueryFilter? LastFilter { get; private set; }
        public long TotalCount { get; set; } = 10;
        public IReadOnlyDictionary<string, long> RiskCounts { get; set; } = new Dictionary<string, long> { ["Unknown"] = 1, ["Low"] = 2 };
        public IReadOnlyDictionary<string, long> RetryActionCounts { get; set; } = new Dictionary<string, long> { ["RetryWithBackoff"] = 7, ["MoveToDeadLetter"] = 1 };
        public double AverageConfidenceScore { get; set; } = 0.7d;
        public IReadOnlyList<AiDashboardRecentFindingResult> RecentFindings { get; set; } = [new() { Id = "analysis", FindingType = "Analysis", Title = "Analysis", Summary = "Summary", CreatedAtUtc = NowUtc.AddMinutes(-3) }];
        public bool ThrowOnCount { get; set; }

        public Task InsertAsync(AiAnalysisResult result, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AiAnalysisResult?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<AiAnalysisResult?>(null);
        public Task<AiAnalysisResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default) => Task.FromResult<AiAnalysisResult?>(null);
        public Task<IReadOnlyList<AiAnalysisResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnalysisResult>>([]);
        public Task<IReadOnlyList<AiAnalysisResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnalysisResult>>([]);

        public Task<long> CountByDateRangeAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default)
        {
            LastFilter = filter;
            if (ThrowOnCount)
            {
                throw new InvalidOperationException("Repository count failed.");
            }

            return Task.FromResult(TotalCount);
        }

        public Task<IReadOnlyDictionary<string, long>> CountByRiskLevelAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default) => Task.FromResult(RiskCounts);
        public Task<IReadOnlyDictionary<string, long>> CountByRetryActionAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default) => Task.FromResult(RetryActionCounts);
        public Task<double> GetAverageConfidenceScoreAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default) => Task.FromResult(AverageConfidenceScore);
        public Task<IReadOnlyList<AiDashboardRecentFindingResult>> GetRecentFindingsAsync(AiDashboardQueryFilter filter, int limit, CancellationToken cancellationToken = default) => Task.FromResult(RecentFindings);
    }

    private sealed class FakeAiAnomalyRecordRepository : IAiAnomalyRecordRepository
    {
        public long TotalCount { get; set; } = 4;
        public IReadOnlyDictionary<string, long> RiskCounts { get; set; } = new Dictionary<string, long> { ["Medium"] = 3 };
        public IReadOnlyDictionary<string, long> AnomalyTypeCounts { get; set; } = new Dictionary<string, long> { ["RateLimitSpike"] = 3, ["TimeoutSpike"] = 1 };
        public IReadOnlyList<AiDashboardRecentFindingResult> RecentFindings { get; set; } = [new() { Id = "anomaly", FindingType = "Anomaly", Title = "Rate limit spike detected", Summary = "HTTP 429 responses increased sharply.", CreatedAtUtc = NowUtc.AddMinutes(-2) }];

        public Task<AiAnomalyRecordRepositoryResult> InsertAsync(AiAnomalyRecord record, CancellationToken cancellationToken = default) => Task.FromResult(AiAnomalyRecordRepositoryResult.Success(record));
        public Task<AiAnomalyRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<AiAnomalyRecord?>(null);
        public Task<AiAnomalyRecord?> GetByAnomalyIdAsync(string anomalyId, CancellationToken cancellationToken = default) => Task.FromResult<AiAnomalyRecord?>(null);
        public Task<IReadOnlyList<AiAnomalyRecord>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnomalyRecord>>([]);
        public Task<IReadOnlyList<AiAnomalyRecord>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnomalyRecord>>([]);
        public Task<IReadOnlyList<AiAnomalyRecord>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnomalyRecord>>([]);
        public Task<IReadOnlyList<AiAnomalyRecord>> GetBySubscriptionIdAsync(string subscriptionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnomalyRecord>>([]);
        public Task<IReadOnlyList<AiAnomalyRecord>> GetByEndpointIdAsync(string endpointId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnomalyRecord>>([]);
        public Task<IReadOnlyList<AiAnomalyRecord>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnomalyRecord>>([]);
        public Task<IReadOnlyList<AiAnomalyRecord>> SearchAsync(HookBridge.AI.Worker.DTOs.AiAnomalyRecordSearchRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnomalyRecord>>([]);
        public Task<long> CountByDateRangeAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default) => Task.FromResult(TotalCount);
        public Task<IReadOnlyDictionary<string, long>> CountByRiskLevelAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default) => Task.FromResult(RiskCounts);
        public Task<IReadOnlyDictionary<string, long>> CountByAnomalyTypeAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default) => Task.FromResult(AnomalyTypeCounts);
        public Task<IReadOnlyList<AiDashboardRecentFindingResult>> GetRecentFindingsAsync(AiDashboardQueryFilter filter, int limit, CancellationToken cancellationToken = default) => Task.FromResult(RecentFindings);
    }

    private sealed class FakeAiSecurityAnalysisRepository : IAiSecurityAnalysisRepository
    {
        public long TotalCount { get; set; } = 2;
        public IReadOnlyDictionary<string, long> RiskCounts { get; set; } = new Dictionary<string, long> { ["High"] = 4, ["Critical"] = 5 };
        public double AverageConfidenceScore { get; set; } = 0.9d;
        public IReadOnlyList<AiDashboardRecentFindingResult> RecentFindings { get; set; } = [new() { Id = "security", FindingType = "Security", Title = "Suspicious webhook activity detected", Summary = "Signature failures increased.", CreatedAtUtc = NowUtc.AddMinutes(-1) }];

        public Task InsertAsync(AiSecurityAnalysisResult result, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AiSecurityAnalysisResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default) => Task.FromResult<AiSecurityAnalysisResult?>(null);
        public Task<IReadOnlyList<AiSecurityAnalysisResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiSecurityAnalysisResult>>([]);
        public Task<IReadOnlyList<AiSecurityAnalysisResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiSecurityAnalysisResult>>([]);
        public Task<IReadOnlyList<AiSecurityAnalysisResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiSecurityAnalysisResult>>([]);
        public Task<IReadOnlyList<AiSecurityAnalysisResult>> SearchAsync(HookBridge.AI.Worker.DTOs.AiSecurityAnalysisSearchRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiSecurityAnalysisResult>>([]);
        public Task<long> CountByDateRangeAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default) => Task.FromResult(TotalCount);
        public Task<IReadOnlyDictionary<string, long>> CountByRiskLevelAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default) => Task.FromResult(RiskCounts);
        public Task<double> GetAverageConfidenceScoreAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default) => Task.FromResult(AverageConfidenceScore);
        public Task<IReadOnlyList<AiDashboardRecentFindingResult>> GetRecentFindingsAsync(AiDashboardQueryFilter filter, int limit, CancellationToken cancellationToken = default) => Task.FromResult(RecentFindings);
    }

    private sealed class FakeCustomerEndpointRiskScoreRepository : ICustomerEndpointRiskScoreRepository
    {
        public long HighRiskEndpointCount { get; set; } = 3;
        public IReadOnlyDictionary<string, long> HealthStatusCounts { get; set; } = new Dictionary<string, long> { ["Healthy"] = 6, ["Unhealthy"] = 4 };

        public Task InsertAsync(CustomerEndpointRiskScoreResult result, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomerEndpointRiskScoreResult>>([]);
        public Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> GetBySubscriptionIdAsync(string subscriptionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomerEndpointRiskScoreResult>>([]);
        public Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> GetByEndpointIdAsync(string endpointId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomerEndpointRiskScoreResult>>([]);
        public Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomerEndpointRiskScoreResult>>([]);
        public Task<long> CountHighRiskEndpointsAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default) => Task.FromResult(HighRiskEndpointCount);
        public Task<IReadOnlyDictionary<string, long>> CountByHealthStatusAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default) => Task.FromResult(HealthStatusCounts);
    }
}
