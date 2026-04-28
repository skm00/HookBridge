using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HookBridge.Api.Swagger;

public sealed class SwaggerSecurityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasAllowAnonymous = context.MethodInfo.GetCustomAttribute<AllowAnonymousAttribute>() is not null
            || context.MethodInfo.DeclaringType?.GetCustomAttribute<AllowAnonymousAttribute>() is not null;

        if (hasAllowAnonymous)
        {
            operation.Security?.Clear();
            return;
        }

        var relativePath = context.ApiDescription.RelativePath ?? string.Empty;
        if (relativePath.Contains("/events/{tenantId}", StringComparison.OrdinalIgnoreCase))
        {
            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "ApiKey",
                        },
                    }] = Array.Empty<string>(),
                },
            ];

            return;
        }

        var hasAuthorize = context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any()
            || context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any() == true;

        if (!hasAuthorize)
        {
            return;
        }

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer",
                    },
                }] = Array.Empty<string>(),
            },
        ];
    }
}

public sealed class SwaggerTagOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var controller = (context.ApiDescription.ActionDescriptor as ControllerActionDescriptor)?.ControllerName ?? string.Empty;
        var path = context.ApiDescription.RelativePath?.ToLowerInvariant() ?? string.Empty;

        var tag = controller switch
        {
            "Auth" => "Auth",
            "Events" => "Events",
            "Tenants" => "Tenants",
            "ApiKeys" => "API Keys",
            "Subscriptions" => "Subscriptions",
            "DeliveryLogs" => "Delivery Logs",
            "FailedEvents" => "Failed Events",
            "Notifications" => "Notifications",
            "Billing" => "Billing",
            "Usage" => "Usage",
            "Dashboard" => "Dashboard",
            "IncomingEvents" => "Events",
            "AuditLogs" => "Dashboard",
            _ when path.Contains("/health") || path == "health" => "Health",
            _ => null,
        };

        if (tag is null)
        {
            return;
        }

        operation.Tags = [new OpenApiTag { Name = tag }];
    }
}

public sealed class SwaggerExamplesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var method = context.MethodInfo.Name;
        var controller = (context.ApiDescription.ActionDescriptor as ControllerActionDescriptor)?.ControllerName;

        if (controller == "Auth" && method == "RegisterAsync")
        {
            SetJsonBodyExample(operation, "{\"email\":\"owner@acme.com\",\"password\":\"StrongPassword123!\",\"fullName\":\"Acme Owner\",\"tenantName\":\"Acme Inc\",\"tenantSlug\":\"acme\"}");
        }

        if (controller == "Auth" && method == "LoginAsync")
        {
            SetJsonBodyExample(operation, "{\"email\":\"owner@acme.com\",\"password\":\"StrongPassword123!\"}");
        }

        if (controller == "ApiKeys" && method == "CreateAsync")
        {
            SetJsonBodyExample(operation, "{\"name\":\"CI Pipeline Key\",\"expiresAt\":\"2026-12-31T23:59:59Z\"}");
        }

        if (controller == "Subscriptions" && method == "CreateAsync")
        {
            SetJsonBodyExample(operation, "{\"tenantId\":\"tenant_123\",\"eventType\":\"order.created\",\"targetUrl\":\"https://example.com/webhooks/orders\",\"isActive\":true}");
        }

        if (controller == "Events" && method == "IngestAsync")
        {
            SetJsonBodyExample(operation, "{\"eventId\":\"evt_1001\",\"eventType\":\"order.created\",\"data\":{\"orderId\":\"ord_123\",\"amount\":149.50}}");
        }

        if (controller == "DeliveryLogs" && method == "SearchAsync")
        {
            operation.Parameters ??= [];
            foreach (var parameter in operation.Parameters.Where(p => p.Name is "eventId" or "status" or "pageNumber" or "pageSize"))
            {
                parameter.Example = parameter.Name switch
                {
                    "eventId" => new OpenApiString("evt_1001"),
                    "status" => new OpenApiString("Delivered"),
                    "pageNumber" => new OpenApiInteger(1),
                    "pageSize" => new OpenApiInteger(50),
                    _ => parameter.Example,
                };
            }
        }

        if (controller == "FailedEvents" && method == "RetryAsync")
        {
            operation.Description = "Manually retries a DLQ failed event by id.";
        }
    }

    private static void SetJsonBodyExample(OpenApiOperation operation, string json)
    {
        if (operation.RequestBody?.Content.TryGetValue("application/json", out var mediaType) == true)
        {
            mediaType.Example = OpenApiAnyFactory.CreateFromJson(json);
        }
    }
}

public sealed class SwaggerCommonResponsesOperationFilter : IOperationFilter
{
    private static readonly Dictionary<string, string> CommonResponses = new()
    {
        ["200"] = "OK",
        ["201"] = "Created",
        ["202"] = "Accepted",
        ["204"] = "No Content",
        ["400"] = "Bad Request",
        ["401"] = "Unauthorized",
        ["403"] = "Forbidden",
        ["404"] = "Not Found",
        ["409"] = "Conflict",
        ["429"] = "Too Many Requests",
        ["500"] = "Internal Server Error",
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Responses ??= new OpenApiResponses();
        foreach (var (statusCode, description) in CommonResponses)
        {
            if (!operation.Responses.ContainsKey(statusCode))
            {
                operation.Responses[statusCode] = new OpenApiResponse { Description = description };
            }
        }
    }
}

public sealed class SwaggerSensitiveSchemaFilter : ISchemaFilter
{
    private static readonly HashSet<string> SensitiveFields =
    [
        "KeyHash",
        "PasswordHash",
        "ClientSecret",
        "HmacSecret",
        "Secret",
        "SecretKey",
        "WebhookSecret",
        "JwtSecret",
        "MasterKey",
        "EncryptionMasterKey",
    ];

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema.Properties.Count == 0)
        {
            return;
        }

        var toRemove = schema.Properties
            .Where(x => SensitiveFields.Contains(x.Key, StringComparer.OrdinalIgnoreCase))
            .Select(x => x.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            schema.Properties.Remove(key);
        }
    }
}
