using System.Text.Json;
using HookBridge.Application.DTOs.AiNaturalLanguageQuery;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.PromptVersioning;

namespace HookBridge.Api.Services.AiNaturalLanguageQuery;

public sealed class AiNaturalLanguageQueryPromptBuilder : IAiNaturalLanguageQueryPromptBuilder
{
    private readonly IAiPromptVersionProvider? _promptVersionProvider;

    public AiNaturalLanguageQueryPromptBuilder(IAiPromptVersionProvider? promptVersionProvider = null)
    {
        _promptVersionProvider = promptVersionProvider;
    }

    public string BuildPrompt(AiNaturalLanguageQueryRequestDto request, AiNaturalLanguageQueryIntent intent, IReadOnlyList<AiNaturalLanguageQueryResultDto> results)
    {
        var payload = JsonSerializer.Serialize(new
        {
            query = request.Query,
            intent = intent.ToString(),
            resultCount = results.Count,
            results = results.Select(result => new
            {
                result.Id,
                result.EventId,
                result.CorrelationId,
                result.CustomerId,
                result.SubscriptionId,
                result.EndpointId,
                result.ResultType,
                result.Title,
                result.Summary,
                result.RiskLevel,
                result.SuggestedAction,
                result.CreatedAtUtc
            })
        });

        return $$"""
You are HookBridge AI. Answer a user's natural language question about webhook delivery operations using only the provided safe metadata.

Safety rules:
- Return strict JSON only with properties: answer (string), suggestedActions (array of strings), confidenceScore (number from 0 to 1).
- Do not invent missing data. If evidence is insufficient, say so.
- Keep all suggested actions advisory and safe. Never claim that you executed an action.
- Never expose secrets, headers, tokens, raw payloads, target credentials, connection strings, or generated database queries.
- Do not produce MongoDB queries or instructions to bypass access controls.

Safe input data:
{{payload}}
""";
    }

    public async Task<AiPromptBuildResult> BuildPromptWithMetadataAsync(AiNaturalLanguageQueryRequestDto request, AiNaturalLanguageQueryIntent intent, IReadOnlyList<AiNaturalLanguageQueryResultDto> results, CancellationToken cancellationToken = default)
    {
        var content = BuildPrompt(request, intent, results);
        var metadata = new AiPromptVersionInfoDto
        {
            PromptName = AiPromptNames.NaturalLanguageQuery,
            Version = AiPromptOptions.DefaultPromptVersion
        };

        if (_promptVersionProvider is not null)
        {
            metadata = (await _promptVersionProvider.GetPromptAsync(AiPromptNames.NaturalLanguageQuery, cancellationToken: cancellationToken)).Metadata;
        }

        return new AiPromptBuildResult { Content = content, Metadata = metadata };
    }

}
