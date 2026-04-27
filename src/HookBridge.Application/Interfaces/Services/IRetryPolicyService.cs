using HookBridge.Application.DTOs.Subscriptions;

namespace HookBridge.Application.Interfaces.Services;

public interface IRetryPolicyService
{
    TimeSpan CalculateDelay(RetryPolicyDto retryPolicy, int attemptNumber);

    bool ShouldRetry(RetryPolicyDto retryPolicy, int attemptNumber);
}
