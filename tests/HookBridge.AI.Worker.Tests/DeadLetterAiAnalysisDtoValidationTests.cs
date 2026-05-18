using System.ComponentModel.DataAnnotations;
using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Tests;

public sealed class DeadLetterAiAnalysisDtoValidationTests
{
    [Fact]
    public void RequestValidation_ReturnsExpectedErrors_ForInvalidFields()
    {
        var request = new DeadLetterAiAnalysisRequestDto
        {
            DeadLetterId = " ",
            EventId = " ",
            RetryCount = -1,
            MaxRetryCount = -1,
            StatusCode = 99,
            TargetUrl = "not-a-url",
            LastRetryAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local),
            FailedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local),
            MovedToDeadLetterAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local)
        };

        var results = request.Validate(new ValidationContext(request)).ToList();

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(DeadLetterAiAnalysisRequestDto.DeadLetterId)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(DeadLetterAiAnalysisRequestDto.EventId)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(DeadLetterAiAnalysisRequestDto.RetryCount)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(DeadLetterAiAnalysisRequestDto.MaxRetryCount)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(DeadLetterAiAnalysisRequestDto.StatusCode)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(DeadLetterAiAnalysisRequestDto.TargetUrl)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(DeadLetterAiAnalysisRequestDto.LastRetryAtUtc)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(DeadLetterAiAnalysisRequestDto.FailedAtUtc)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(DeadLetterAiAnalysisRequestDto.MovedToDeadLetterAtUtc)));
    }

    [Fact]
    public void RequestValidation_AllowsValidOptionalUrlAndStatus()
    {
        var request = new DeadLetterAiAnalysisRequestDto
        {
            DeadLetterId = "dlq_1",
            EventId = "evt_1",
            RetryCount = 0,
            MaxRetryCount = 0,
            StatusCode = 599,
            TargetUrl = "https://customer.example.com/webhook",
            LastRetryAtUtc = DateTime.UtcNow,
            FailedAtUtc = DateTime.UtcNow,
            MovedToDeadLetterAtUtc = DateTime.UtcNow
        };

        Assert.Empty(request.Validate(new ValidationContext(request)));
    }

    [Fact]
    public void ResponseValidation_ReturnsErrors_ForInvalidConfidenceAndNonUtcGeneratedAt()
    {
        var response = new DeadLetterAiAnalysisResponseDto
        {
            ConfidenceScore = 1.1,
            GeneratedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local)
        };

        var results = response.Validate(new ValidationContext(response)).ToList();

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(DeadLetterAiAnalysisResponseDto.ConfidenceScore)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(DeadLetterAiAnalysisResponseDto.GeneratedAtUtc)));
    }

    [Fact]
    public void SearchRequest_DefaultLimitSupportsRepositoryPaging()
    {
        var request = new DeadLetterAiAnalysisSearchRequestDto();

        Assert.Equal(100, request.Limit);
        Assert.Null(request.ReplaySafety);
        Assert.Null(request.SuggestedAction);
    }
}
