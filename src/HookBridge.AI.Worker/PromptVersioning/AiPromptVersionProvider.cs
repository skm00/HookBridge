using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.PromptVersioning;

public sealed class AiPromptVersionProvider : IAiPromptVersionProvider
{
    private static readonly DateTime DefaultCreatedAtUtc = new(2026, 5, 14, 10, 30, 0, DateTimeKind.Utc);

    private readonly AiPromptOptions _options;
    private readonly IHostEnvironment? _environment;
    private readonly ILogger<AiPromptVersionProvider> _logger;

    public AiPromptVersionProvider(IOptions<AiPromptOptions> options, ILogger<AiPromptVersionProvider> logger, IHostEnvironment? environment = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger;
        _environment = environment;
        ValidateOptions();
    }

    public AiPromptTemplateDto GetPrompt(string promptName, string? version = null)
    {
        var selectedVersion = SelectVersion(promptName, version);
        var path = ResolvePromptPath(promptName, selectedVersion);
        var content = File.ReadAllText(path);
        var metadata = CreateMetadata(promptName, selectedVersion, content);

        _logger.LogInformation("Prompt loaded. PromptName: {PromptName}, PromptVersion: {PromptVersion}, PromptHash: {PromptHash}", promptName, selectedVersion, metadata.Hash);
        return new AiPromptTemplateDto { Content = content, Metadata = metadata };
    }

    public async Task<AiPromptTemplateDto> GetPromptAsync(string promptName, string? version = null, CancellationToken cancellationToken = default)
    {
        var selectedVersion = SelectVersion(promptName, version);
        var path = ResolvePromptPath(promptName, selectedVersion);
        var content = await File.ReadAllTextAsync(path, cancellationToken);
        var metadata = CreateMetadata(promptName, selectedVersion, content);

        _logger.LogInformation("Prompt loaded. PromptName: {PromptName}, PromptVersion: {PromptVersion}, PromptHash: {PromptHash}", promptName, selectedVersion, metadata.Hash);
        return new AiPromptTemplateDto { Content = content, Metadata = metadata };
    }

    public IReadOnlyList<AiPromptVersionInfoDto> ListPromptVersions(bool includeContent = false)
    {
        var roots = CandidatePromptRoots().Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var versions = new List<AiPromptVersionInfoDto>();

        foreach (var promptName in AiPromptNames.All.OrderBy(name => name, StringComparer.Ordinal))
        {
            foreach (var root in roots)
            {
                var promptDirectory = Path.Combine(root, promptName);
                if (!Directory.Exists(promptDirectory))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(promptDirectory, "*.prompt.txt"))
                {
                    var version = Path.GetFileName(file).Replace(".prompt.txt", string.Empty, StringComparison.Ordinal);
                    if (!AiPromptOptions.IsValidVersion(version))
                    {
                        continue;
                    }

                    var content = File.ReadAllText(file);
                    var metadata = CreateMetadata(promptName, version, content, includeContent);
                    versions.Add(metadata);
                }

                break;
            }
        }

        return versions
            .DistinctBy(item => $"{item.PromptName}:{item.Version}")
            .OrderBy(item => item.PromptName, StringComparer.Ordinal)
            .ThenBy(item => item.Version, StringComparer.Ordinal)
            .ToArray();
    }

    public AiPromptVersionInfoDto GetPromptMetadata(string promptName, string version, bool includeContent = false)
    {
        if (!AiPromptOptions.IsValidVersion(version))
        {
            throw new ArgumentException($"Prompt version '{version}' must follow semantic format like v1.0.0.", nameof(version));
        }

        var prompt = GetPrompt(promptName, version);
        if (includeContent)
        {
            prompt.Metadata.Content = prompt.Content;
        }

        return prompt.Metadata;
    }

    private string SelectVersion(string promptName, string? version)
    {
        if (string.IsNullOrWhiteSpace(promptName))
        {
            _logger.LogError("Missing prompt error. PromptName was not provided.");
            throw new KeyNotFoundException("Prompt name is required.");
        }

        if (!AiPromptNames.IsKnown(promptName))
        {
            _logger.LogError("Missing prompt error. PromptName: {PromptName} is not known.", promptName);
            throw new KeyNotFoundException($"Prompt name '{promptName}' is not known.");
        }

        var selectedVersion = string.IsNullOrWhiteSpace(version)
            ? (_options.Prompts.TryGetValue(promptName, out var configuredVersion) ? configuredVersion : _options.DefaultVersion)
            : version;

        _logger.LogInformation("Prompt version selected. PromptName: {PromptName}, PromptVersion: {PromptVersion}", promptName, selectedVersion);

        if (string.IsNullOrWhiteSpace(selectedVersion))
        {
            throw new KeyNotFoundException($"Prompt version for '{promptName}' is missing.");
        }

        if (!AiPromptOptions.IsValidVersion(selectedVersion))
        {
            throw new ArgumentException($"Prompt version '{selectedVersion}' must follow semantic format like v1.0.0.", nameof(version));
        }

        return selectedVersion;
    }

    private string ResolvePromptPath(string promptName, string version)
    {
        var relativePath = Path.Combine(promptName, $"{version}.prompt.txt");
        foreach (var root in CandidatePromptRoots())
        {
            var path = Path.Combine(root, relativePath);
            if (File.Exists(path))
            {
                return path;
            }
        }

        _logger.LogError("Missing prompt error. PromptName: {PromptName}, PromptVersion: {PromptVersion}", promptName, version);
        throw new FileNotFoundException($"Prompt file was not found for prompt '{promptName}' version '{version}'.", relativePath);
    }

    private IEnumerable<string> CandidatePromptRoots()
    {
        if (!string.IsNullOrWhiteSpace(_environment?.ContentRootPath))
        {
            yield return Path.Combine(_environment.ContentRootPath, "Prompts");
            yield return Path.Combine(_environment.ContentRootPath, "..", "HookBridge.AI.Worker", "Prompts");
        }

        yield return Path.Combine(AppContext.BaseDirectory, "Prompts");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "Prompts");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "src", "HookBridge.AI.Worker", "Prompts");
    }

    private AiPromptVersionInfoDto CreateMetadata(string promptName, string version, string content, bool includeContent = false)
    {
        var hash = $"sha256:{Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content))).ToLowerInvariant()}";
        _logger.LogDebug("Prompt hash calculated. PromptName: {PromptName}, PromptVersion: {PromptVersion}, PromptHash: {PromptHash}", promptName, version, hash);

        return new AiPromptVersionInfoDto
        {
            PromptName = promptName,
            Version = version,
            Description = $"Default {SplitName(promptName).ToLowerInvariant()} prompt.",
            CreatedAtUtc = DefaultCreatedAtUtc,
            IsActive = string.Equals(SelectConfiguredVersion(promptName), version, StringComparison.Ordinal),
            Hash = hash,
            Content = includeContent ? content : null
        };
    }

    private string SelectConfiguredVersion(string promptName)
        => _options.Prompts.TryGetValue(promptName, out var version) ? version : _options.DefaultVersion;

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.DefaultVersion))
        {
            throw new OptionsValidationException(nameof(AiPromptOptions), typeof(AiPromptOptions), ["AIPrompts:DefaultVersion is required."]);
        }

        if (!AiPromptOptions.IsValidVersion(_options.DefaultVersion))
        {
            throw new OptionsValidationException(nameof(AiPromptOptions), typeof(AiPromptOptions), ["AIPrompts:DefaultVersion must follow semantic format like v1.0.0."]);
        }

        foreach (var (promptName, version) in _options.Prompts)
        {
            if (!AiPromptNames.IsKnown(promptName))
            {
                throw new OptionsValidationException(nameof(AiPromptOptions), typeof(AiPromptOptions), [$"AIPrompts:Prompts contains unknown prompt name '{promptName}'."]);
            }

            if (!AiPromptOptions.IsValidVersion(version))
            {
                throw new OptionsValidationException(nameof(AiPromptOptions), typeof(AiPromptOptions), [$"AIPrompts:Prompts:{promptName} must follow semantic format like v1.0.0."]);
            }
        }
    }

    private static string SplitName(string promptName)
        => string.Concat(promptName.Select((character, index) => index > 0 && char.IsUpper(character) ? " " + character : character.ToString()));
}
