using System.Text.Json;
using FluentAssertions;
using FluentValidation.TestHelper;
using HookBridge.Application.DTOs.Events;
using HookBridge.Application.Validation.Events;
using HookBridge.Shared.Constants;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class EventIngestionRequestDtoValidatorCoverageTests
{
    private readonly EventIngestionRequestDtoValidator _validator = new();

    [Fact]
    public void Validate_WhenPayloadIsJsonObject_Passes()
    {
        using var document = JsonDocument.Parse("{\"orderId\":123}");
        var request = new EventIngestionRequestDto
        {
            EventType = "order.created_v2",
            EventId = "evt-1",
            Data = document.RootElement.Clone(),
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenPayloadIsJsonArray_FailsPayloadShapeValidation()
    {
        using var document = JsonDocument.Parse("[1,2,3]");
        var request = new EventIngestionRequestDto
        {
            EventType = "order.created",
            EventId = "evt-1",
            Data = document.RootElement.Clone(),
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Data)
            .WithErrorMessage("Payload must be a JSON object.");
    }

    [Fact]
    public void Validate_WhenEventTypeHasUnsupportedCharacters_Fails()
    {
        var request = new EventIngestionRequestDto
        {
            EventType = "order created!",
            EventId = "evt-1",
            Data = new { orderId = 123 },
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.EventType)
            .WithErrorMessage("EventType may contain only letters, numbers, dot (.), dash (-), and underscore (_).");
    }

    [Fact]
    public void Validate_WhenPayloadExceedsLimit_FailsSizeValidation()
    {
        var request = new EventIngestionRequestDto
        {
            EventType = "order.created",
            EventId = "evt-1",
            Data = new { value = new string('x', ValidationLimits.MaxPayloadSizeBytes) },
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Data)
            .WithErrorMessage($"Data payload exceeds {ValidationLimits.MaxPayloadSizeBytes} bytes.");
    }

    [Fact]
    public void PayloadProperty_MapsToDataForRawWebhookPayloads()
    {
        var request = new EventIngestionRequestDto();
        var payload = new { value = "from-payload-alias" };

        request.Payload = payload;

        request.Data.Should().BeSameAs(payload);
        request.Payload.Should().BeSameAs(payload);
    }
}
