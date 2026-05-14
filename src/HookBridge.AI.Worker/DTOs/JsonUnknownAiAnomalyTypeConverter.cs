using System.Text.Json;
using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

public sealed class JsonUnknownAiAnomalyTypeConverter : JsonConverter<AiAnomalyType>
{
    public override AiAnomalyType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            return Enum.TryParse<AiAnomalyType>(value, ignoreCase: true, out var parsed)
                ? parsed
                : AiAnomalyType.Unknown;
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numericValue))
        {
            return Enum.IsDefined(typeof(AiAnomalyType), numericValue)
                ? (AiAnomalyType)numericValue
                : AiAnomalyType.Unknown;
        }

        return AiAnomalyType.Unknown;
    }

    public override void Write(Utf8JsonWriter writer, AiAnomalyType value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
