using HookBridge.Domain.Entities;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;

namespace HookBridge.Infrastructure.Persistence.Indexes;

/// <summary>
/// Creates required MongoDB indexes at startup.
/// </summary>
public sealed class MongoIndexInitializerHostedService(IMongoDatabase database) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var tenants = database.GetCollection<Tenant>(nameof(Tenant));

        var slugIndexKeys = Builders<Tenant>.IndexKeys.Ascending(x => x.Slug);
        var slugIndexOptions = new CreateIndexOptions { Unique = true, Name = "ux_tenant_slug" };
        var slugIndex = new CreateIndexModel<Tenant>(slugIndexKeys, slugIndexOptions);

        await tenants.Indexes.CreateOneAsync(slugIndex, cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
