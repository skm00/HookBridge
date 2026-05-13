namespace HookBridge.AI.Worker.Configuration;

/// <summary>
/// MongoDB settings for persisting AI analysis results.
/// </summary>
public sealed class AiMongoOptions
{
    public const string SectionName = "AiMongo";
    public const string DefaultAiAnalysisResultsCollectionName = "ai_analysis_results";

    public string ConnectionString { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = string.Empty;

    public string AiAnalysisResultsCollectionName { get; set; } = DefaultAiAnalysisResultsCollectionName;
}
