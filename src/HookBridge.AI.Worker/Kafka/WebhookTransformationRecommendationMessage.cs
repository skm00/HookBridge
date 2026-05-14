using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public sealed record WebhookTransformationRecommendationMessage(WebhookTransformationRecommendationRequestDto Request, Func<CancellationToken, Task> AcknowledgeAsync);
