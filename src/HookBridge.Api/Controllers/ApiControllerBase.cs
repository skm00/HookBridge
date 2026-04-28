using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected string TraceId => HttpContext.TraceIdentifier;

    protected ActionResult<ApiResponse<T>> OkResponse<T>(T data, string? message = null)
        => Ok(ApiResponseFactory.Success(data, message, TraceId));

    protected ActionResult<ApiResponse<T>> CreatedResponse<T>(string actionName, object? routeValues, T data, string? message = null)
        => CreatedAtAction(actionName, routeValues, ApiResponseFactory.Success(data, message, TraceId));

    protected ActionResult<ApiResponse<T>> AcceptedResponse<T>(T data, string? message = null)
        => Accepted(ApiResponseFactory.Success(data, message, TraceId));

    protected ActionResult<ApiErrorResponse> ErrorResponse(int statusCode, string message, Dictionary<string, string[]>? errors = null)
    {
        var response = errors is null
            ? ApiResponseFactory.Error(message, statusCode, TraceId)
            : ApiResponseFactory.ValidationError(errors, TraceId);

        return statusCode switch
        {
            StatusCodes.Status400BadRequest => BadRequest(response),
            StatusCodes.Status401Unauthorized => Unauthorized(response),
            StatusCodes.Status403Forbidden => StatusCode(StatusCodes.Status403Forbidden, response),
            StatusCodes.Status404NotFound => NotFound(response),
            StatusCodes.Status409Conflict => Conflict(response),
            StatusCodes.Status429TooManyRequests => StatusCode(StatusCodes.Status429TooManyRequests, response),
            _ => StatusCode(statusCode, response),
        };
    }
}
