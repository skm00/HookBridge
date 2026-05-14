using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HookBridge.AI.Worker.Services.DuplicateReplayDetection;

public sealed class WebhookFingerprintHashService : IWebhookFingerprintHashService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    public string? GeneratePayloadHash(object? payload)
    {
        if (payload is null) return null;
        var text = payload switch
        {
            string s => s,
            JsonElement element => element.GetRawText(),
            _ => JsonSerializer.Serialize(payload, SerializerOptions)
        };
        if (string.IsNullOrWhiteSpace(text)) return null;
        return $"sha256:{Hash(NormalizeJsonOrRaw(text))}";
    }

    public string? GenerateSignatureHash(string? signature)
        => string.IsNullOrWhiteSpace(signature) ? null : $"sha256:{Hash(signature.Trim())}";

    private static string NormalizeJsonOrRaw(string value)
    {
        try
        {
            var node = JsonNode.Parse(value);
            return node is null ? value : NormalizeNode(node).ToJsonString(SerializerOptions);
        }
        catch (JsonException)
        {
            return value;
        }
    }

    private static JsonNode? NormalizeNode(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            var normalized = new JsonObject();
            foreach (var property in obj.OrderBy(p => p.Key, StringComparer.Ordinal)) normalized[property.Key] = NormalizeNode(property.Value?.DeepClone());
            return normalized;
        }

        if (node is JsonArray array)
        {
            var normalized = new JsonArray();
            foreach (var item in array) normalized.Add(NormalizeNode(item?.DeepClone()));
            return normalized;
        }

        return node?.DeepClone();
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
