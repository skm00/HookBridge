using HookBridge.AI.Worker.PromptVersioning;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Controllers;

[ApiController]
[Route("api/ai-prompts")]
public sealed class AiPromptsController(
    IAiPromptVersionProvider promptVersionProvider,
    ILogger<AiPromptsController> logger) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AiPromptVersionInfoDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<IReadOnlyList<AiPromptVersionInfoDto>>> ListPromptVersions()
    {
        var prompts = promptVersionProvider.ListPromptVersions(includeContent: false);
        logger.LogInformation("AI prompt metadata list requested. Count={Count}", prompts.Count);
        return OkResponse(prompts);
    }

    [HttpGet("{promptName}/{version}")]
    [ProducesResponseType(typeof(ApiResponse<AiPromptVersionInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public ActionResult<ApiResponse<AiPromptVersionInfoDto>> GetPromptVersion(
        string promptName,
        string version,
        [FromQuery] bool includeContent = false)
    {
        if (!AiPromptOptions.IsValidVersion(version))
        {
            return ErrorResponse<AiPromptVersionInfoDto>(
                StatusCodes.Status400BadRequest,
                "Prompt version must follow semantic format like v1.0.0.");
        }

        try
        {
            var metadata = promptVersionProvider.GetPromptMetadata(promptName, version, includeContent);
            logger.LogInformation("AI prompt metadata requested. PromptName={PromptName} PromptVersion={PromptVersion} IncludeContent={IncludeContent}", promptName, version, includeContent);
            return OkResponse(metadata);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or FileNotFoundException or DirectoryNotFoundException)
        {
            logger.LogInformation(ex, "AI prompt metadata not found. PromptName={PromptName} PromptVersion={PromptVersion}", promptName, version);
            return ErrorResponse<AiPromptVersionInfoDto>(
                StatusCodes.Status404NotFound,
                "Prompt version was not found.");
        }
    }
}
