namespace HookBridge.Application.Interfaces.Services;

public interface IBackupService
{
    Task<byte[]> ExportAsync(string tenantId, CancellationToken cancellationToken = default);

    Task ImportAsync(string tenantId, byte[] data, CancellationToken cancellationToken = default);
}
