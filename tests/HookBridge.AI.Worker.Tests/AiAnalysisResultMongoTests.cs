using FluentAssertions;
using HookBridge.AI.Worker.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiAnalysisResultMongoTests
{
    [Fact]
    public void AiAnalysisResult_SerializesWithExpectedMongoFieldNames()
    {
        var createdAtUtc = DateTime.SpecifyKind(new DateTime(2026, 5, 13, 10, 15, 30), DateTimeKind.Utc);
        var result = new AiAnalysisResult
        {
            Id = ObjectId.GenerateNewId().ToString(),
            EventId = "evt-1",
            CorrelationId = "corr-1",
            Source = "hookbridge.worker",
            EventType = "webhook.delivery.failed",
            FailureReason = "HTTP 500",
            AiSummary = "Endpoint failed after retries.",
            RootCause = "Target service outage.",
            AiRecommendation = "Inspect endpoint logs.",
            RiskLevel = "High",
            ConfidenceScore = 0.87,
            SuggestedRetryAction = "RetryWithBackoff",
            IsRetryRecommended = true,
            Model = "llama3",
            Provider = "Ollama",
            CreatedAtUtc = createdAtUtc
        };

        var document = result.ToBsonDocument();

        document.Should().ContainKey("_id");
        document["eventId"].AsString.Should().Be("evt-1");
        document["correlationId"].AsString.Should().Be("corr-1");
        document["source"].AsString.Should().Be("hookbridge.worker");
        document["eventType"].AsString.Should().Be("webhook.delivery.failed");
        document["failureReason"].AsString.Should().Be("HTTP 500");
        document["aiSummary"].AsString.Should().Be("Endpoint failed after retries.");
        document["rootCause"].AsString.Should().Be("Target service outage.");
        document["aiRecommendation"].AsString.Should().Be("Inspect endpoint logs.");
        document["riskLevel"].AsString.Should().Be("High");
        document["confidenceScore"].AsDouble.Should().Be(0.87);
        document["suggestedRetryAction"].AsString.Should().Be("RetryWithBackoff");
        document["isRetryRecommended"].AsBoolean.Should().BeTrue();
        document["model"].AsString.Should().Be("llama3");
        document["provider"].AsString.Should().Be("Ollama");
        BsonSerializer.Deserialize<AiAnalysisResult>(document).CreatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }
}
