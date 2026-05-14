using System.Net;
using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;

namespace HookBridge.AI.Worker.Tests;

public sealed class SemanticKernelLocalLlmClientTests
{
    [Fact]
    public async Task GenerateAsync_WithSuccessfulResponse_ReturnsSuccess()
    {
        var client = CreateClient(kernel: CreateKernel(new FakeChatCompletionService("{\"ok\":true}")));

        var result = await client.GenerateAsync("Analyze event evt-123.");

        result.IsSuccess.Should().BeTrue();
        result.ResponseText.Should().Be("{\"ok\":true}");
        result.FallbackReason.Should().Be(AiFallbackReason.None);
    }

    [Fact]
    public async Task GenerateAsync_WhenProviderTimesOut_ReturnsTimeoutFallback()
    {
        var client = CreateClient(factoryException: new TimeoutException("request timed out"));

        var result = await client.GenerateAsync("prompt");

        result.IsSuccess.Should().BeFalse();
        result.FallbackReason.Should().Be(AiFallbackReason.Timeout);
        result.ErrorMessage.Should().Be("LLM provider request timed out.");
    }

    [Fact]
    public async Task GenerateAsync_WhenConnectionFails_ReturnsProviderUnavailableFallback()
    {
        var client = CreateClient(factoryException: new HttpRequestException("connection refused"));

        var result = await client.GenerateAsync("prompt");

        result.IsSuccess.Should().BeFalse();
        result.FallbackReason.Should().Be(AiFallbackReason.ProviderUnavailable);
        result.ErrorMessage.Should().Be("LLM provider connection failed.");
    }

    [Fact]
    public async Task GenerateAsync_WhenProviderReturnsNonSuccessStatus_ReturnsProviderUnavailableFallback()
    {
        var client = CreateClient(factoryException: new HttpRequestException("server error", null, HttpStatusCode.InternalServerError));

        var result = await client.GenerateAsync("prompt");

        result.IsSuccess.Should().BeFalse();
        result.FallbackReason.Should().Be(AiFallbackReason.ProviderUnavailable);
        result.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GenerateAsync_WhenProviderReturnsEmptyResponse_ReturnsInvalidResponseFallback()
    {
        var client = CreateClient(kernel: CreateKernel(new FakeChatCompletionService("   ")));

        var result = await client.GenerateAsync("prompt");

        result.IsSuccess.Should().BeFalse();
        result.FallbackReason.Should().Be(AiFallbackReason.InvalidResponse);
        result.ErrorMessage.Should().Be("LLM provider returned an empty response.");
    }

    [Fact]
    public async Task GenerateAsync_WhenModelIsUnavailable_ReturnsModelUnavailableFallback()
    {
        var client = CreateClient(factoryException: new InvalidOperationException("model llama3-missing not found; pull it first"));

        var result = await client.GenerateAsync("prompt");

        result.IsSuccess.Should().BeFalse();
        result.FallbackReason.Should().Be(AiFallbackReason.ModelUnavailable);
        result.ErrorMessage.Should().Be("Configured LLM model is unavailable.");
    }

    [Fact]
    public async Task GenerateAsync_DoesNotLogSensitivePromptData()
    {
        const string sensitivePrompt = "Authorization: Bearer super-secret-token";
        var logger = new TestLogger<SemanticKernelLocalLlmClient>();
        var client = CreateClient(factoryException: new HttpRequestException("connection refused"), logger: logger);

        await client.GenerateAsync(sensitivePrompt);

        logger.Records.Select(record => record.Message).Should().NotContain(message => message.Contains("super-secret-token", StringComparison.Ordinal));
        logger.Records.SelectMany(record => record.Properties.Values).OfType<string>().Should().NotContain(value => value.Contains("super-secret-token", StringComparison.Ordinal));
    }

    private static SemanticKernelLocalLlmClient CreateClient(
        Kernel? kernel = null,
        Exception? factoryException = null,
        TestLogger<SemanticKernelLocalLlmClient>? logger = null)
    {
        var factory = new Mock<IKernelFactory>(MockBehavior.Strict);
        if (factoryException is null)
        {
            factory.Setup(item => item.CreateKernel()).Returns(kernel ?? CreateKernel(new FakeChatCompletionService("ok")));
        }
        else
        {
            factory.Setup(item => item.CreateKernel()).Throws(factoryException);
        }

        var options = Options.Create(new AiOptions
        {
            Enabled = true,
            Provider = "Ollama",
            Model = "llama3-test",
            Endpoint = "http://localhost:11434",
            MaxRetries = 0,
            LlmRequestTimeoutSeconds = 1
        });

        return new SemanticKernelLocalLlmClient(factory.Object, options, logger ?? new TestLogger<SemanticKernelLocalLlmClient>());
    }

    private static Kernel CreateKernel(IChatCompletionService chatCompletionService)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatCompletionService);
        return builder.Build();
    }

    private sealed class FakeChatCompletionService : IChatCompletionService
    {
        private readonly string _response;

        public FakeChatCompletionService(string response)
        {
            _response = response;
        }

        public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ChatMessageContent> response = [new ChatMessageContent(AuthorRole.Assistant, _response)];
            return Task.FromResult(response);
        }

        public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
            => EmptyStreamingResponse();

        private static async IAsyncEnumerable<StreamingChatMessageContent> EmptyStreamingResponse()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
