using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.PromptVersioning;

public static class AiPromptMetadataExtensions
{
    public static void ApplyPromptMetadata(this WebhookFailureAnalysisResponseDto response, AiPromptVersionInfoDto metadata)
        => Apply(response, metadata);

    public static void ApplyPromptMetadata(this AiLogSummaryResponseDto response, AiPromptVersionInfoDto metadata)
        => Apply(response, metadata);

    public static void ApplyPromptMetadata(this PayloadSchemaDetectionResponseDto response, AiPromptVersionInfoDto metadata)
        => Apply(response, metadata);

    public static void ApplyPromptMetadata(this JsonToDtoSuggestionResponseDto response, AiPromptVersionInfoDto metadata)
        => Apply(response, metadata);

    public static void ApplyPromptMetadata(this FluentValidationRuleGenerationResponseDto response, AiPromptVersionInfoDto metadata)
        => Apply(response, metadata);

    public static void ApplyPromptMetadata(this WebhookTransformationRecommendationResponseDto response, AiPromptVersionInfoDto metadata)
        => Apply(response, metadata);

    public static void ApplyPromptMetadata(this AiSecurityAnalysisResponseDto response, AiPromptVersionInfoDto metadata)
        => Apply(response, metadata);

    private static void Apply(dynamic response, AiPromptVersionInfoDto metadata)
    {
        response.PromptName = metadata.PromptName;
        response.PromptVersion = metadata.Version;
        response.PromptHash = metadata.Hash;
    }
}
