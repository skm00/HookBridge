using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HookBridge.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected string? TraceId => ControllerContext?.HttpContext?.TraceIdentifier;

    protected ActionResult<ApiResponse<T>> OkResponse<T>(T data, string? message = null)
        => Ok(ApiResponseFactory.Success(data, message, TraceId));

    protected ActionResult<ApiResponse<T>> CreatedResponse<T>(string actionName, object? routeValues, T data, string? message = null)
    {
        var values = routeValues is null ? new RouteValueDictionary() : new RouteValueDictionary(routeValues);
        if (!values.ContainsKey("version") && ControllerContext?.RouteData?.Values.TryGetValue("version", out var version) == true)
        {
            values["version"] = version;
        }

        return CreatedAtAction(actionName, values, ApiResponseFactory.Success(data, message, TraceId));
    }

    protected ActionResult<ApiResponse<T>> AcceptedResponse<T>(T data, string? message = null)
        => Accepted(ApiResponseFactory.Success(data, message, TraceId));

    protected IActionResult ErrorResponse(int statusCode, string message, Dictionary<string, string[]>? errors = null)
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

    protected ActionResult<ApiResponse<T>> ErrorResponse<T>(int statusCode, string message, Dictionary<string, string[]>? errors = null)
        => statusCode switch
        {
            StatusCodes.Status400BadRequest => BadRequest(errors is null
                ? ApiResponseFactory.Error(message, statusCode, TraceId)
                : ApiResponseFactory.ValidationError(errors, TraceId)),
            StatusCodes.Status401Unauthorized => Unauthorized(ApiResponseFactory.Error(message, statusCode, TraceId)),
            StatusCodes.Status403Forbidden => StatusCode(StatusCodes.Status403Forbidden, ApiResponseFactory.Error(message, statusCode, TraceId)),
            StatusCodes.Status404NotFound => NotFound(ApiResponseFactory.Error(message, statusCode, TraceId)),
            StatusCodes.Status409Conflict => Conflict(ApiResponseFactory.Error(message, statusCode, TraceId)),
            StatusCodes.Status429TooManyRequests => StatusCode(StatusCodes.Status429TooManyRequests, ApiResponseFactory.Error(message, statusCode, TraceId)),
            _ => StatusCode(statusCode, ApiResponseFactory.Error(message, statusCode, TraceId)),
        };
}
