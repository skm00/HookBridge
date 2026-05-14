namespace HookBridge.AI.Worker.Mongo;

public interface IPayloadSchemaDetectionRepository
{
    Task InsertAsync(PayloadSchemaDetectionResult result, CancellationToken cancellationToken = default);
}
