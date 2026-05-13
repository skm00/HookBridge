using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Bson;

namespace HookBridge.Worker.KafkaSwapBuffer;

internal sealed class BsonValueJsonConverter : JsonConverter<BsonValue>
{
    public override BsonValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var rawJson = document.RootElement.GetRawText();
        return BsonDocument.Parse($"{{\"value\":{rawJson}}}")["value"];
    }

    public override void Write(Utf8JsonWriter writer, BsonValue value, JsonSerializerOptions options)
    {
        using var document = JsonDocument.Parse(value.ToJson());
        document.RootElement.WriteTo(writer);
    }
}
