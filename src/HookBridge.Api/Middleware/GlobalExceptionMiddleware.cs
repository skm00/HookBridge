using System.Text.Json;
using FluentValidation;
using HookBridge.Application.Exceptions;
using HookBridge.Shared.Api;

namespace HookBridge.Api.Middleware;

/// <summary>
/// Global exception handling middleware that returns a standardized JSON payload.
/// </summary>
public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    /// <summary>
    /// Invokes the middleware pipeline and handles unhandled exceptions.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException validationException)
        {
            var errors = validationException.Errors
                .GroupBy(x => x.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).Distinct().ToArray());

            await WriteErrorResponseAsync(
                context,
                StatusCodes.Status400BadRequest,
                ApiResponseFactory.ValidationError(errors, context.TraceIdentifier));
        }
        catch (ConflictException conflictException)
        {
            await WriteErrorResponseAsync(context, StatusCodes.Status409Conflict,
                ApiResponseFactory.Error(conflictException.Message, StatusCodes.Status409Conflict, context.TraceIdentifier));
        }
        catch (KeyNotFoundException keyNotFoundException)
        {
            await WriteErrorResponseAsync(context, StatusCodes.Status404NotFound,
                ApiResponseFactory.Error(keyNotFoundException.Message, StatusCodes.Status404NotFound, context.TraceIdentifier));
        }
        catch (UnauthorizedException unauthorizedException)
        {
            await WriteErrorResponseAsync(context, StatusCodes.Status401Unauthorized,
                ApiResponseFactory.Error(unauthorizedException.Message, StatusCodes.Status401Unauthorized, context.TraceIdentifier));
        }
        catch (ForbiddenException forbiddenException)
        {
            await WriteErrorResponseAsync(context, StatusCodes.Status403Forbidden,
                ApiResponseFactory.Error(forbiddenException.Message, StatusCodes.Status403Forbidden, context.TraceIdentifier));
        }
        catch (TooManyRequestsException tooManyRequestsException)
        {
            await WriteErrorResponseAsync(context, StatusCodes.Status429TooManyRequests,
                ApiResponseFactory.Error(tooManyRequestsException.Message, StatusCodes.Status429TooManyRequests, context.TraceIdentifier));
        }
        catch (InvalidOperationException invalidOperationException)
        {
            await WriteErrorResponseAsync(context, StatusCodes.Status500InternalServerError,
                ApiResponseFactory.Error(invalidOperationException.Message, StatusCodes.Status500InternalServerError, context.TraceIdentifier));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}. TraceId: {TraceId}", context.Request.Method, context.Request.Path, context.TraceIdentifier);
            await WriteErrorResponseAsync(context, StatusCodes.Status500InternalServerError,
                ApiResponseFactory.Error("An unexpected error occurred.", StatusCodes.Status500InternalServerError, context.TraceIdentifier));
        }
    }

    private static Task WriteErrorResponseAsync(HttpContext context, int statusCode, ApiErrorResponse response)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
