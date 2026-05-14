using System.ComponentModel.DataAnnotations;

namespace HookBridge.AI.Worker.Configuration;

public sealed class DuplicateReplayDetectionOptions
{
    public const string SectionName = "DuplicateReplayDetection";

    public bool Enabled { get; set; } = true;

    [Range(1, int.MaxValue)] public int FingerprintTtlHours { get; set; } = 72;
    [Range(1, int.MaxValue)] public int ReplayWindowMinutes { get; set; } = 15;
    [Range(0, int.MaxValue)] public int FutureTimestampToleranceMinutes { get; set; } = 5;
    [Range(1, int.MaxValue)] public int HighFrequencyThreshold { get; set; } = 5;
    [Range(1, int.MaxValue)] public int HighFrequencyWindowSeconds { get; set; } = 60;
    public string HashAlgorithm { get; set; } = "SHA256";
}
