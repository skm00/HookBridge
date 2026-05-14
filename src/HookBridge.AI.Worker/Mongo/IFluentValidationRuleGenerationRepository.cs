namespace HookBridge.AI.Worker.Mongo;

public interface IFluentValidationRuleGenerationRepository
{
    Task InsertAsync(FluentValidationRuleGenerationResult result, CancellationToken cancellationToken = default);
}
