using HookBridge.Application.DTOs.DeliveryAttempts;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Services;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class DeliveryAttemptServiceTests
{
    [Fact]
    public async Task Search_ByTenantId_ReturnsMatchingRecords()
    {
        var service = CreateService();

        var result = await service.SearchAsync(new DeliveryAttemptSearchRequestDto { TenantId = "tenant-1" });

        Assert.All(result.Items, x => Assert.Equal("tenant-1", x.TenantId));
    }

    [Fact]
    public async Task Search_ByEventId_ReturnsMatchingRecords()
    {
        var service = CreateService();

        var result = await service.SearchAsync(new DeliveryAttemptSearchRequestDto { EventId = "evt-1" });

        Assert.All(result.Items, x => Assert.Equal("evt-1", x.EventId));
    }

    [Fact]
    public async Task Search_BySubscriptionId_ReturnsMatchingRecords()
    {
        var service = CreateService();

        var result = await service.SearchAsync(new DeliveryAttemptSearchRequestDto { SubscriptionId = "sub-1" });

        Assert.All(result.Items, x => Assert.Equal("sub-1", x.SubscriptionId));
    }

    [Fact]
    public async Task Search_ByStatus_ReturnsMatchingRecords()
    {
        var service = CreateService();

        var result = await service.SearchAsync(new DeliveryAttemptSearchRequestDto { Status = DeliveryStatus.Failed });

        Assert.All(result.Items, x => Assert.Equal(DeliveryStatus.Failed, x.Status));
    }

    [Fact]
    public async Task Search_ByHttpStatusCode_ReturnsMatchingRecords()
    {
        var service = CreateService();

        var result = await service.SearchAsync(new DeliveryAttemptSearchRequestDto { HttpStatusCode = 500 });

        Assert.All(result.Items, x => Assert.Equal(500, x.HttpStatusCode));
    }

    [Fact]
    public async Task Search_ByDateRange_ReturnsMatchingRecords()
    {
        var service = CreateService();

        var from = new DateTime(2026, 4, 27, 10, 10, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 4, 27, 10, 20, 0, DateTimeKind.Utc);
        var result = await service.SearchAsync(new DeliveryAttemptSearchRequestDto { FromDate = from, ToDate = to });

        Assert.All(result.Items, x => Assert.InRange(x.AttemptedAt, from, to));
    }

    [Fact]
    public async Task Search_ByTargetUrlContains_ReturnsMatchingRecords()
    {
        var service = CreateService();

        var result = await service.SearchAsync(new DeliveryAttemptSearchRequestDto { TargetUrl = "EXAMPLE.com/orders" });

        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, x => Assert.Contains("example.com/orders", x.TargetUrl, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Search_ReturnsNewestFirst()
    {
        var service = CreateService();

        var result = await service.SearchAsync(new DeliveryAttemptSearchRequestDto());

        var ordered = result.Items.OrderByDescending(x => x.AttemptedAt).Select(x => x.Id).ToArray();
        Assert.Equal(ordered, result.Items.Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task Search_LimitsResultsTo500()
    {
        var service = new DeliveryAttemptService(new FakeDeliveryAttemptRepository(BuildLargeSeed(650)));

        var result = await service.SearchAsync(new DeliveryAttemptSearchRequestDto());

        Assert.Equal(500, result.PageSize);
    }

    [Fact]
    public async Task GetById_ReturnsRecord_WhenFound()
    {
        var service = CreateService();

        var result = await service.GetByIdAsync("attempt-1");

        Assert.NotNull(result);
        Assert.Equal("attempt-1", result!.Id);
    }

    [Fact]
    public async Task GetById_ReturnsNull_WhenNotFound()
    {
        var service = CreateService();

        var result = await service.GetByIdAsync("missing");

        Assert.Null(result);
    }

    private static DeliveryAttemptService CreateService()
    {
        return new DeliveryAttemptService(new FakeDeliveryAttemptRepository(BuildSeed()));
    }

    private static IReadOnlyList<DeliveryAttempt> BuildSeed() =>
    [
        BuildAttempt("attempt-1", "tenant-1", "evt-1", "sub-1", DeliveryStatus.Success, 200, "https://example.com/orders", new DateTime(2026, 4, 27, 10, 05, 0, DateTimeKind.Utc)),
        BuildAttempt("attempt-2", "tenant-1", "evt-2", "sub-2", DeliveryStatus.Failed, 500, "https://example.com/orders/v2", new DateTime(2026, 4, 27, 10, 15, 0, DateTimeKind.Utc)),
        BuildAttempt("attempt-3", "tenant-2", "evt-1", "sub-1", DeliveryStatus.Pending, null, "https://api.vendor.com/hook", new DateTime(2026, 4, 27, 10, 25, 0, DateTimeKind.Utc)),
    ];

    private static IReadOnlyList<DeliveryAttempt> BuildLargeSeed(int count)
        => Enumerable.Range(1, count)
            .Select(i => BuildAttempt(
                $"attempt-{i}",
                "tenant-1",
                $"evt-{i}",
                "sub-1",
                DeliveryStatus.Success,
                200,
                "https://example.com/orders",
                new DateTime(2026, 4, 27, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i)))
            .ToList();

    private static DeliveryAttempt BuildAttempt(
        string id,
        string tenantId,
        string eventId,
        string subscriptionId,
        DeliveryStatus status,
        int? httpStatusCode,
        string targetUrl,
        DateTime attemptedAt)
    {
        return new DeliveryAttempt
        {
            Id = id,
            TenantId = tenantId,
            EventId = eventId,
            SubscriptionId = subscriptionId,
            EventType = "order.created",
            TargetUrl = targetUrl,
            AttemptNumber = 1,
            Status = status,
            HttpStatusCode = httpStatusCode,
            DurationMs = 120,
            AttemptedAt = attemptedAt,
            CorrelationId = "corr-1",
            CreatedAt = attemptedAt,
            UpdatedAt = null,
        };
    }

    private sealed class FakeDeliveryAttemptRepository(IReadOnlyList<DeliveryAttempt> seed) : IDeliveryAttemptRepository
    {
        private readonly IReadOnlyList<DeliveryAttempt> _items = seed;

        public Task<(IReadOnlyList<DeliveryAttempt> Items, long TotalCount)> SearchAsync(DeliveryAttemptSearchRequestDto request, MongoDB.Driver.SortDefinition<DeliveryAttempt> sort, int skip, int limit, CancellationToken cancellationToken = default)
        {
            IEnumerable<DeliveryAttempt> query = _items;

            if (!string.IsNullOrWhiteSpace(request.TenantId))
            {
                query = query.Where(x => x.TenantId == request.TenantId);
            }

            if (!string.IsNullOrWhiteSpace(request.EventId))
            {
                query = query.Where(x => x.EventId == request.EventId);
            }

            if (!string.IsNullOrWhiteSpace(request.SubscriptionId))
            {
                query = query.Where(x => x.SubscriptionId == request.SubscriptionId);
            }

            if (!string.IsNullOrWhiteSpace(request.EventType))
            {
                query = query.Where(x => x.EventType == request.EventType);
            }

            if (request.Status.HasValue)
            {
                query = query.Where(x => x.Status == request.Status.Value);
            }

            if (request.HttpStatusCode.HasValue)
            {
                query = query.Where(x => x.HttpStatusCode == request.HttpStatusCode.Value);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(x => x.AttemptedAt >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(x => x.AttemptedAt <= request.ToDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.TargetUrl))
            {
                query = query.Where(x => x.TargetUrl.Contains(request.TargetUrl, StringComparison.OrdinalIgnoreCase));
            }

            var list = query.ToList();
            var result = list.Skip(skip).Take(limit).ToList();

            return Task.FromResult<(IReadOnlyList<DeliveryAttempt>, long)>((result, list.LongCount()));
        }

        public Task<DeliveryAttempt?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));

        public Task<long> CountAsync(string tenantId, DateTime fromDateInclusive, DateTime toDateExclusive, DeliveryStatus? status, CancellationToken cancellationToken = default)
        {
            IEnumerable<DeliveryAttempt> query = _items.Where(x =>
                x.TenantId == tenantId &&
                x.AttemptedAt >= fromDateInclusive &&
                x.AttemptedAt < toDateExclusive);

            if (status.HasValue)
            {
                query = query.Where(x => x.Status == status.Value);
            }

            return Task.FromResult(query.LongCount());
        }
    }
}
