using HookBridge.Application.DTOs.ApiKeys;
using HookBridge.Application.DTOs.EndpointValidation;
using HookBridge.Application.DTOs.Events;
using HookBridge.Application.DTOs.Subscriptions;

namespace HookBridge.Application.Tests;

internal static class TestDataBuilders
{
    public static CreateSubscriptionRequestDto CreateSubscriptionRequest(
        string targetUrl = "https://webhooks.example.com/orders",
        string eventType = "order.created") => new()
    {
        EventType = eventType,
        TargetUrl = targetUrl,
        Headers = [new KeyValueDto { Name = "x-source", Value = "hookbridge-tests" }],
        RetryPolicy = RetryPolicy(),
        TimeoutSeconds = 30,
        DeliveryFormat = "Raw",
    };

    public static UpdateSubscriptionRequestDto UpdateSubscriptionRequest(
        string targetUrl = "https://webhooks.example.com/orders-updated",
        string eventType = "order.updated") => new()
    {
        EventType = eventType,
        TargetUrl = targetUrl,
        Headers = [new KeyValueDto { Name = "x-updated", Value = "true" }],
        RetryPolicy = RetryPolicy(maxAttempts: 5, initialDelaySeconds: 20, backoffType: "Fixed"),
        TimeoutSeconds = 45,
        DeliveryFormat = "Raw",
    };

    public static RetryPolicyDto RetryPolicy(int maxAttempts = 3, int initialDelaySeconds = 10, string backoffType = "Exponential") => new()
    {
        MaxAttempts = maxAttempts,
        InitialDelaySeconds = initialDelaySeconds,
        BackoffType = backoffType,
    };

    public static EventIngestionRequestDto WebhookEventRequest(string eventType = "order.created") => new()
    {
        EventType = eventType,
        EventId = "evt_test_123",
        Data = new { orderId = "ord_123" },
    };

    public static EndpointValidationRequestDto EndpointValidationRequest(string targetUrl = "https://webhooks.example.com/health") => new()
    {
        TargetUrl = targetUrl,
        TimeoutSeconds = 10,
    };

    public static CreateApiKeyRequestDto CreateApiKeyRequest(string name = "Primary API") => new()
    {
        Name = name,
        EnableSignatureValidation = false,
        SignatureHeaderName = "x-hookbridge-signature",
    };
}
