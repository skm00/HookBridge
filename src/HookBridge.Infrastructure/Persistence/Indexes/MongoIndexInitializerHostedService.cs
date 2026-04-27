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

        var apiKeys = database.GetCollection<ApiKey>(nameof(ApiKey));

        var hashUniqueIndex = new CreateIndexModel<ApiKey>(
            Builders<ApiKey>.IndexKeys.Ascending(x => x.KeyHash),
            new CreateIndexOptions { Unique = true, Name = "ux_apikey_keyhash" });

        var tenantIndex = new CreateIndexModel<ApiKey>(
            Builders<ApiKey>.IndexKeys.Ascending(x => x.TenantId),
            new CreateIndexOptions { Name = "ix_apikey_tenantid" });

        var tenantActiveIndex = new CreateIndexModel<ApiKey>(
            Builders<ApiKey>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.IsActive),
            new CreateIndexOptions { Name = "ix_apikey_tenantid_isactive" });

        await apiKeys.Indexes.CreateManyAsync([hashUniqueIndex, tenantIndex, tenantActiveIndex], cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
