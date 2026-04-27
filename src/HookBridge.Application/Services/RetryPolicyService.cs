using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Application.Interfaces.Services;

namespace HookBridge.Application.Services;

public sealed class RetryPolicyService : IRetryPolicyService
{
    public TimeSpan CalculateDelay(RetryPolicyDto retryPolicy, int attemptNumber)
    {
        ArgumentNullException.ThrowIfNull(retryPolicy);

        var normalizedAttempt = Math.Max(1, attemptNumber);
        var initialDelaySeconds = Math.Max(0, retryPolicy.InitialDelaySeconds);

        if (string.Equals(retryPolicy.BackoffType, "Exponential", StringComparison.OrdinalIgnoreCase))
        {
            var multiplier = Math.Pow(2, normalizedAttempt - 1);
            return TimeSpan.FromSeconds(initialDelaySeconds * multiplier);
        }

        return TimeSpan.FromSeconds(initialDelaySeconds);
    }

    public bool ShouldRetry(RetryPolicyDto retryPolicy, int attemptNumber)
    {
        ArgumentNullException.ThrowIfNull(retryPolicy);
        return attemptNumber < retryPolicy.MaxAttempts;
    }
}
