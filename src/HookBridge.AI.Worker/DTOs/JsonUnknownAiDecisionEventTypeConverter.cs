using System.Text.Json;
using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

public sealed class JsonUnknownAiDecisionEventTypeConverter : JsonConverter<AiDecisionEventType>
{
    public override AiDecisionEventType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            return Enum.TryParse<AiDecisionEventType>(value, ignoreCase: true, out var parsed)
                ? parsed
                : AiDecisionEventType.Unknown;
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numericValue))
        {
            return Enum.IsDefined(typeof(AiDecisionEventType), numericValue)
                ? (AiDecisionEventType)numericValue
                : AiDecisionEventType.Unknown;
        }

        return AiDecisionEventType.Unknown;
    }

    public override void Write(Utf8JsonWriter writer, AiDecisionEventType value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
