namespace HookBridge.Infrastructure.Configuration;

/// <summary>
/// Represents MongoDB connection configuration.
/// </summary>
public sealed class MongoDbSettings
{
    /// <summary>
    /// Gets or sets the MongoDB connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MongoDB database name.
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;
}
