using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Application.Services;

namespace HookBridge.Application.Tests;

public sealed class RetryPolicyServiceTests
{
    private readonly RetryPolicyService _service = new();

    [Fact]
    public void CalculateDelay_FixedBackoff_ReturnsInitialDelay()
    {
        var retryPolicy = new RetryPolicyDto
        {
            MaxAttempts = 3,
            InitialDelaySeconds = 15,
            BackoffType = "Fixed",
        };

        var delay = _service.CalculateDelay(retryPolicy, attemptNumber: 2);

        Assert.Equal(TimeSpan.FromSeconds(15), delay);
    }

    [Fact]
    public void CalculateDelay_ExponentialBackoff_ReturnsExponentiallyIncreasedDelay()
    {
        var retryPolicy = new RetryPolicyDto
        {
            MaxAttempts = 5,
            InitialDelaySeconds = 10,
            BackoffType = "Exponential",
        };

        var delay = _service.CalculateDelay(retryPolicy, attemptNumber: 3);

        Assert.Equal(TimeSpan.FromSeconds(40), delay);
    }

    [Fact]
    public void ShouldRetry_AttemptLowerThanMaxAttempts_ReturnsTrue()
    {
        var retryPolicy = new RetryPolicyDto
        {
            MaxAttempts = 4,
            InitialDelaySeconds = 10,
            BackoffType = "Fixed",
        };

        var shouldRetry = _service.ShouldRetry(retryPolicy, attemptNumber: 2);

        Assert.True(shouldRetry);
    }

    [Fact]
    public void ShouldRetry_AttemptAtOrAboveMaxAttempts_ReturnsFalse()
    {
        var retryPolicy = new RetryPolicyDto
        {
            MaxAttempts = 4,
            InitialDelaySeconds = 10,
            BackoffType = "Fixed",
        };

        Assert.False(_service.ShouldRetry(retryPolicy, attemptNumber: 4));
        Assert.False(_service.ShouldRetry(retryPolicy, attemptNumber: 5));
    }
}
