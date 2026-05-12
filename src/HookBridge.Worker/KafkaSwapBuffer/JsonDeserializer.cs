using System.Text.Json;
using Confluent.Kafka;

namespace HookBridge.Worker.KafkaSwapBuffer;

/// <summary>
/// System.Text.Json Kafka value deserializer for strongly typed webhook event payloads.
/// </summary>
public sealed class JsonDeserializer<T> : IDeserializer<T>
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
        options.Converters.Add(new BsonValueJsonConverter());
        return options;
    }

    public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
    {
        if (isNull)
        {
            throw new JsonException($"Kafka message value for {typeof(T).Name} was null.");
        }

        return JsonSerializer.Deserialize<T>(data, SerializerOptions)
            ?? throw new JsonException($"Kafka message value could not be deserialized as {typeof(T).Name}.");
    }
}
