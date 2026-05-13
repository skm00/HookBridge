namespace HookBridge.AI.Worker.Configuration;

public sealed class AiOptions
{
    public const string SectionName = "AI";

    public bool Enabled { get; set; } = true;

    public string Provider { get; set; } = "Ollama";

    public string Model { get; set; } = "llama3";

    public string Endpoint { get; set; } = "http://localhost:11434";
}
