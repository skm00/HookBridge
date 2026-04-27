using System.Text.Json;
using FluentValidation;
using HookBridge.Application.Exceptions;

namespace HookBridge.Api.Middleware;

/// <summary>
/// Global exception handling middleware that returns a standardized JSON payload.
/// </summary>
public sealed class GlobalExceptionMiddleware(RequestDelegate next)
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
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var response = new
            {
                message = "Validation failed.",
                errors = validationException.Errors.Select(x => new { x.PropertyName, x.ErrorMessage }),
                traceId = context.TraceIdentifier,
                statusCode = StatusCodes.Status400BadRequest,
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
        catch (ConflictException conflictException)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";

            var response = new
            {
                message = conflictException.Message,
                traceId = context.TraceIdentifier,
                statusCode = StatusCodes.Status409Conflict,
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
        catch (KeyNotFoundException keyNotFoundException)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/json";

            var response = new
            {
                message = keyNotFoundException.Message,
                traceId = context.TraceIdentifier,
                statusCode = StatusCodes.Status404NotFound,
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
        catch (UnauthorizedException unauthorizedException)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";

            var response = new
            {
                message = unauthorizedException.Message,
                traceId = context.TraceIdentifier,
                statusCode = StatusCodes.Status401Unauthorized,
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
        catch (TooManyRequestsException tooManyRequestsException)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";

            var response = new
            {
                message = tooManyRequestsException.Message,
                traceId = context.TraceIdentifier,
                statusCode = StatusCodes.Status429TooManyRequests,
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
        catch (Exception)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var response = new
            {
                message = "An unexpected error occurred.",
                traceId = context.TraceIdentifier,
                statusCode = StatusCodes.Status500InternalServerError,
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
