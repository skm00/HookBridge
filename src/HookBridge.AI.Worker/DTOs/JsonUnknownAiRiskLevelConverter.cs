using System.Text.Json;
using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

public sealed class JsonUnknownAiRiskLevelConverter : JsonConverter<AiRiskLevel>
{
    public override AiRiskLevel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            return Enum.TryParse<AiRiskLevel>(value, ignoreCase: true, out var parsed)
                ? parsed
                : AiRiskLevel.Unknown;
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numericValue))
        {
            return Enum.IsDefined(typeof(AiRiskLevel), numericValue)
                ? (AiRiskLevel)numericValue
                : AiRiskLevel.Unknown;
        }

        return AiRiskLevel.Unknown;
    }

    public override void Write(Utf8JsonWriter writer, AiRiskLevel value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
