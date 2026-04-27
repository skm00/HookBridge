using System.Text.Json;

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
