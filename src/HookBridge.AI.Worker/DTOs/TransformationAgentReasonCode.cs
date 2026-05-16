using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<TransformationAgentReasonCode>))]
public enum TransformationAgentReasonCode
{
    Unknown,
    DirectMappingAvailable,
    RenameMappingAvailable,
    TypeConversionRequired,
    DateFormatConversionRequired,
    MissingRequiredTargetField,
    UnmappedImportantSourceField,
    InvalidSourceJson,
    InvalidTargetJson,
    LowConfidenceMapping,
    GeneratedCodeRequiresApproval,
    ManualReviewRequired
}
