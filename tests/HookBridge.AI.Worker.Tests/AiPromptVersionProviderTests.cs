using FluentAssertions;
using HookBridge.AI.Worker.PromptVersioning;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiPromptVersionProviderTests
{
    [Fact]
    public void GetPrompt_LoadsDefaultVersionAndStableHash()
    {
        using var prompts = CreatePrompts((AiPromptNames.WebhookFailureAnalysis, "v1.0.0", "default prompt"));
        var provider = CreateProvider(prompts.Root);

        var first = provider.GetPrompt(AiPromptNames.WebhookFailureAnalysis);
        var second = provider.GetPrompt(AiPromptNames.WebhookFailureAnalysis);

        first.Content.Should().Be("default prompt");
        first.Metadata.Version.Should().Be("v1.0.0");
        first.Metadata.Hash.Should().StartWith("sha256:");
        second.Metadata.Hash.Should().Be(first.Metadata.Hash);
    }

    [Fact]
    public void GetPrompt_LoadsSpecificVersion()
    {
        using var prompts = CreatePrompts(
            (AiPromptNames.AiLogSummary, "v1.0.0", "old prompt"),
            (AiPromptNames.AiLogSummary, "v1.1.0", "new prompt"));
        var provider = CreateProvider(prompts.Root);

        var prompt = provider.GetPrompt(AiPromptNames.AiLogSummary, "v1.1.0");

        prompt.Content.Should().Be("new prompt");
        prompt.Metadata.IsActive.Should().BeFalse();
    }

    [Fact]
    public void GetPrompt_MissingPromptNameThrowsClearError()
    {
        using var prompts = CreatePrompts();
        var provider = CreateProvider(prompts.Root);

        Action act = () => provider.GetPrompt("UnknownPrompt");

        act.Should().Throw<KeyNotFoundException>().WithMessage("*not known*");
    }

    [Fact]
    public void GetPrompt_MissingPromptVersionThrowsClearError()
    {
        using var prompts = CreatePrompts((AiPromptNames.JsonToDtoSuggestion, "v1.0.0", "prompt"));
        var provider = CreateProvider(prompts.Root);

        Action act = () => provider.GetPrompt(AiPromptNames.JsonToDtoSuggestion, "v9.9.9");

        act.Should().Throw<FileNotFoundException>().WithMessage("*v9.9.9*");
    }

    [Fact]
    public void GetPrompt_InvalidVersionFormatThrowsClearError()
    {
        using var prompts = CreatePrompts((AiPromptNames.PayloadSchemaDetection, "v1.0.0", "prompt"));
        var provider = CreateProvider(prompts.Root);

        Action act = () => provider.GetPrompt(AiPromptNames.PayloadSchemaDetection, "1.0.0");

        act.Should().Throw<ArgumentException>().WithMessage("*v1.0.0*");
    }

    [Fact]
    public void GetPrompt_HashChangesWhenContentChanges()
    {
        using var prompts = CreatePrompts(
            (AiPromptNames.AiSecurityAnalysis, "v1.0.0", "first"),
            (AiPromptNames.AiSecurityAnalysis, "v1.1.0", "second"));
        var provider = CreateProvider(prompts.Root);

        var first = provider.GetPrompt(AiPromptNames.AiSecurityAnalysis, "v1.0.0");
        var second = provider.GetPrompt(AiPromptNames.AiSecurityAnalysis, "v1.1.0");

        second.Metadata.Hash.Should().NotBe(first.Metadata.Hash);
    }

    [Fact]
    public void AiPromptOptions_BindsConfiguredVersions()
    {
        var options = new AiPromptOptions
        {
            DefaultVersion = "v1.0.0",
            Prompts = new Dictionary<string, string> { [AiPromptNames.WebhookFailureAnalysis] = "v1.1.0" }
        };

        options.DefaultVersion.Should().Be("v1.0.0");
        options.Prompts[AiPromptNames.WebhookFailureAnalysis].Should().Be("v1.1.0");
        AiPromptOptions.IsValidVersion("v2.0.0").Should().BeTrue();
    }

    private static AiPromptVersionProvider CreateProvider(string contentRoot)
        => new(
            Options.Create(new AiPromptOptions()),
            NullLogger<AiPromptVersionProvider>.Instance,
            new TestHostEnvironment(contentRoot));

    private static TempPromptRoot CreatePrompts(params (string PromptName, string Version, string Content)[] prompts)
    {
        var root = Path.Combine(Path.GetTempPath(), "hookbridge-prompts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        foreach (var (promptName, version, content) in prompts)
        {
            var directory = Path.Combine(root, "Prompts", promptName);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, $"{version}.prompt.txt"), content);
        }

        return new TempPromptRoot(root);
    }

    private sealed class TempPromptRoot(string root) : IDisposable
    {
        public string Root { get; } = root;
        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "HookBridge.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
