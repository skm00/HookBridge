using System.Text.Json;
using HookBridge.Application.Messaging;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class KafkaMessageContractsTests
{
    [Fact]
    public void WebhookEventMessage_SerializesCorrectly()
    {
        var payload = new WebhookEventMessage
        {
            EventId = "evt_001",
            TenantId = "tenant_001",
            EventType = "order.created",
            ReceivedAt = new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc),
            CorrelationId = "corr_001",
        };

        var json = JsonSerializer.Serialize(payload);
        var roundTrip = JsonSerializer.Deserialize<WebhookEventMessage>(json);

        Assert.NotNull(roundTrip);
        Assert.Equal(payload.EventId, roundTrip!.EventId);
        Assert.Equal(payload.TenantId, roundTrip.TenantId);
        Assert.Equal(payload.EventType, roundTrip.EventType);
        Assert.Equal(payload.ReceivedAt, roundTrip.ReceivedAt);
        Assert.Equal(payload.CorrelationId, roundTrip.CorrelationId);
    }

    [Fact]
    public void WebhookRetryMessage_SerializesCorrectly()
    {
        var payload = new WebhookRetryMessage
        {
            EventId = "evt_002",
            TenantId = "tenant_002",
            SubscriptionId = "sub_001",
            FailedEventId = "failed_001",
            AttemptNumber = 2,
            NextRetryAt = new DateTime(2026, 4, 27, 12, 30, 0, DateTimeKind.Utc),
            CorrelationId = "corr_002",
        };

        var json = JsonSerializer.Serialize(payload);
        var roundTrip = JsonSerializer.Deserialize<WebhookRetryMessage>(json);

        Assert.NotNull(roundTrip);
        Assert.Equal(payload.EventId, roundTrip!.EventId);
        Assert.Equal(payload.TenantId, roundTrip.TenantId);
        Assert.Equal(payload.SubscriptionId, roundTrip.SubscriptionId);
        Assert.Equal(payload.FailedEventId, roundTrip.FailedEventId);
        Assert.Equal(payload.AttemptNumber, roundTrip.AttemptNumber);
        Assert.Equal(payload.NextRetryAt, roundTrip.NextRetryAt);
        Assert.Equal(payload.CorrelationId, roundTrip.CorrelationId);
    }

    [Fact]
    public void WebhookDlqMessage_SerializesCorrectly()
    {
        var payload = new WebhookDlqMessage
        {
            EventId = "evt_003",
            TenantId = "tenant_003",
            SubscriptionId = "sub_003",
            Reason = "max retry attempts exceeded",
            FinalAttemptNumber = 5,
            CorrelationId = "corr_003",
        };

        var json = JsonSerializer.Serialize(payload);
        var roundTrip = JsonSerializer.Deserialize<WebhookDlqMessage>(json);

        Assert.NotNull(roundTrip);
        Assert.Equal(payload.EventId, roundTrip!.EventId);
        Assert.Equal(payload.TenantId, roundTrip.TenantId);
        Assert.Equal(payload.SubscriptionId, roundTrip.SubscriptionId);
        Assert.Equal(payload.Reason, roundTrip.Reason);
        Assert.Equal(payload.FinalAttemptNumber, roundTrip.FinalAttemptNumber);
        Assert.Equal(payload.CorrelationId, roundTrip.CorrelationId);
    }
}
