using System.IO.Compression;
using System.Text.Json;
using Asp.Versioning;
using FluentValidation;
using HookBridge.Api.Authorization;
using HookBridge.Api.RateLimiting;
using HookBridge.Api.Security;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Models;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HookBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[EnableRateLimiting(RateLimitingPolicyNames.AdminApiPolicy)]
[Route("api/v{version:apiVersion}/admin/tenants/{tenantId}")]
public sealed class BackupController(
    IBackupService backupService,
    TenantAccessValidator tenantAccessValidator) : ApiControllerBase
{
    private const long MaxRestoreFileBytes = 10 * 1024 * 1024;

    [HttpGet("backup")]
    [Authorize(Policy = AuthorizationPolicies.OwnerOnly)]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportAsync(string tenantId, CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(tenantId);
        var bytes = await backupService.ExportAsync(tenantId, cancellationToken);
        return File(bytes, "application/gzip", $"hookbridge-{tenantId}-backup.json.gz");
    }

    [HttpPost("restore")]
    [Authorize(Policy = AuthorizationPolicies.OwnerOnly)]
    [RequestSizeLimit(MaxRestoreFileBytes)]
    [ProducesResponseType(typeof(ApiResponse<RestoreSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<RestoreSummaryResponse>>> ImportAsync(
        string tenantId,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(tenantId);

        if (file.Length == 0)
        {
            throw new ValidationException("Restore file is empty.");
        }

        if (file.Length > MaxRestoreFileBytes)
        {
            throw new ValidationException("Restore file exceeds 10MB limit.");
        }

        await using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        var bytes = memoryStream.ToArray();

        var package = Deserialize(bytes);
        if (!string.Equals(package.TenantId, tenantId, StringComparison.Ordinal))
        {
            throw new ValidationException("Backup tenant does not match requested tenant.");
        }

        await backupService.ImportAsync(tenantId, bytes, cancellationToken);

        var response = new RestoreSummaryResponse
        {
            TenantId = tenantId,
            Imported = new RestoreSummaryCounts
            {
                Tenant = 1,
                Subscriptions = package.Subscriptions.Count,
                ApiKeys = package.ApiKeys.Count,
                Events = package.Events.Count,
                FailedEvents = package.FailedEvents.Count,
                Notifications = package.Notifications.Count,
                AuditLogs = package.AuditLogs.Count,
            },
        };

        return OkResponse(response, "Backup restored.");
    }

    private static TenantBackupPackage Deserialize(byte[] data)
    {
        var jsonBytes = data.Length >= 2 && data[0] == 0x1f && data[1] == 0x8b ? Decompress(data) : data;
        return JsonSerializer.Deserialize<TenantBackupPackage>(jsonBytes, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }) ?? throw new ValidationException("Backup payload is invalid.");
    }

    private static byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    public sealed class RestoreSummaryResponse
    {
        public string TenantId { get; set; } = string.Empty;

        public RestoreSummaryCounts Imported { get; set; } = new();
    }

    public sealed class RestoreSummaryCounts
    {
        public int Tenant { get; set; }

        public int Subscriptions { get; set; }

        public int ApiKeys { get; set; }

        public int Events { get; set; }

        public int FailedEvents { get; set; }

        public int Notifications { get; set; }

        public int AuditLogs { get; set; }
    }
}
