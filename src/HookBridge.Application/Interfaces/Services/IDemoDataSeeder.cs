namespace HookBridge.Application.Interfaces.Services;

public interface IDemoDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
