using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Controllers;

[ApiController]
[Route("api/ai-audit")]
public sealed class AiDecisionAuditController(
    IAiDecisionAuditRepository repository,
    ILogger<AiDecisionAuditController> logger) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AiDecisionAuditResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiDecisionAuditResponseDto>>>> SearchAsync([FromQuery] AiDecisionAuditSearchRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            AiDecisionAuditRepository.ValidateSearch(request);
            var records = await repository.SearchAsync(request, cancellationToken);
            logger.LogInformation("AI decision audit search executed. EventId: {EventId}, CorrelationId: {CorrelationId}, CustomerId: {CustomerId}, DecisionType: {DecisionType}, PageNumber: {PageNumber}, PageSize: {PageSize}", request.EventId, request.CorrelationId, request.CustomerId, request.DecisionType, request.PageNumber, request.PageSize);
            return OkResponse<IReadOnlyList<AiDecisionAuditResponseDto>>(records.Select(record => record.ToResponseDto()).ToList());
        }
        catch (ArgumentException ex)
        {
            logger.LogInformation(ex, "Invalid AI decision audit search filters.");
            return ErrorResponse<IReadOnlyList<AiDecisionAuditResponseDto>>(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error searching AI decision audit records.");
            return ErrorResponse<IReadOnlyList<AiDecisionAuditResponseDto>>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while searching AI decision audit records.");
        }
    }

    [HttpGet("{auditId}")]
    [ProducesResponseType(typeof(ApiResponse<AiDecisionAuditResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<AiDecisionAuditResponseDto>>> GetByAuditIdAsync(string auditId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(auditId)) return ErrorResponse<AiDecisionAuditResponseDto>(StatusCodes.Status400BadRequest, "AuditId is required.");
        try
        {
            var record = await repository.GetByAuditIdAsync(auditId, cancellationToken);
            return record is null
                ? ErrorResponse<AiDecisionAuditResponseDto>(StatusCodes.Status404NotFound, "AI decision audit record was not found.")
                : OkResponse(record.ToResponseDto());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error retrieving AI decision audit record. AuditId: {AuditId}", auditId);
            return ErrorResponse<AiDecisionAuditResponseDto>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while retrieving the AI decision audit record.");
        }
    }

    [HttpGet("events/{eventId}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AiDecisionAuditResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiDecisionAuditResponseDto>>>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(eventId)) return ErrorResponse<IReadOnlyList<AiDecisionAuditResponseDto>>(StatusCodes.Status400BadRequest, "EventId is required.");
        try
        {
            var records = await repository.GetByEventIdAsync(eventId, cancellationToken);
            return OkResponse<IReadOnlyList<AiDecisionAuditResponseDto>>(records.Select(record => record.ToResponseDto()).ToList());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error retrieving AI decision audit records. EventId: {EventId}", eventId);
            return ErrorResponse<IReadOnlyList<AiDecisionAuditResponseDto>>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while retrieving AI decision audit records.");
        }
    }

    [HttpGet("correlations/{correlationId}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AiDecisionAuditResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiDecisionAuditResponseDto>>>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) return ErrorResponse<IReadOnlyList<AiDecisionAuditResponseDto>>(StatusCodes.Status400BadRequest, "CorrelationId is required.");
        try
        {
            var records = await repository.GetByCorrelationIdAsync(correlationId, cancellationToken);
            return OkResponse<IReadOnlyList<AiDecisionAuditResponseDto>>(records.Select(record => record.ToResponseDto()).ToList());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error retrieving AI decision audit records. CorrelationId: {CorrelationId}", correlationId);
            return ErrorResponse<IReadOnlyList<AiDecisionAuditResponseDto>>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while retrieving AI decision audit records.");
        }
    }
}
