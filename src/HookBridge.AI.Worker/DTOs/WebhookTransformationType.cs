using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<WebhookTransformationType>))]
public enum WebhookTransformationType
{
    DirectMap,
    Rename,
    TypeConversion,
    DateFormat,
    DefaultValue,
    CombineFields,
    SplitField,
    ConstantValue,
    Conditional,
    Ignore,
    Custom
}
