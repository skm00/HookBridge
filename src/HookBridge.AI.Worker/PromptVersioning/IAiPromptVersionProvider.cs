namespace HookBridge.AI.Worker.PromptVersioning;

public interface IAiPromptVersionProvider
{
    AiPromptTemplateDto GetPrompt(string promptName, string? version = null);

    Task<AiPromptTemplateDto> GetPromptAsync(string promptName, string? version = null, CancellationToken cancellationToken = default);

    IReadOnlyList<AiPromptVersionInfoDto> ListPromptVersions(bool includeContent = false);

    AiPromptVersionInfoDto GetPromptMetadata(string promptName, string version, bool includeContent = false);
}
