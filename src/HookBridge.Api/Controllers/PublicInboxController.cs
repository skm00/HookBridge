using System.Security.Cryptography;
using System.Text;
using Asp.Versioning;
using HookBridge.Api.RateLimiting;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HookBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
public sealed class PublicInboxController(
    IMongoRepository<PublicInbox> inboxRepository,
    IMongoRepository<PublicInboxRequest> requestRepository,
    IDateTimeProvider dateTimeProvider) : ControllerBase
{
    private const int MaxStoredBodyBytes = 64 * 1024;

    [HttpPost("public/inbox")]
    [EnableRateLimiting(RateLimitingPolicyNames.PublicInboxCreationPolicy)]
    public async Task<ActionResult<object>> CreateAsync(CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var inbox = new PublicInbox
        {
            Id = Guid.NewGuid().ToString("n"),
            Token = token,
            MaxRequests = 50,
            RequestCount = 0,
            CreatedAt = now,
            ExpiresAt = now.AddHours(24),
            CreatedByIp = ip,
        };

        await inboxRepository.AddAsync(inbox, cancellationToken);
        return Ok(new
        {
            token = inbox.Token,
            webhookUrl = $"{Request.Scheme}://{Request.Host}/api/v1/webhook/{inbox.Token}",
            expiresAt = inbox.ExpiresAt,
            maxRequests = inbox.MaxRequests,
            remainingRequests = inbox.MaxRequests - inbox.RequestCount,
        });
    }

    [HttpPost("webhook/{token}")]
    [RequestSizeLimit(MaxStoredBodyBytes)]
    public async Task<IActionResult> ReceiveAsync(string token, CancellationToken cancellationToken)
    {
        var inbox = await inboxRepository.FirstOrDefaultAsync(x => x.Token == token, cancellationToken);
        if (inbox is null)
        {
            return NotFound();
        }

        var now = dateTimeProvider.UtcNow;
        if (inbox.ExpiresAt <= now)
        {
            return StatusCode(StatusCodes.Status410Gone, new { message = "Inbox has expired." });
        }

        if (inbox.RequestCount >= inbox.MaxRequests)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "Inbox request limit reached." });
        }

        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(cancellationToken);
        if (Encoding.UTF8.GetByteCount(body) > MaxStoredBodyBytes)
        {
            return BadRequest(new { message = "Payload too large." });
        }

        var headers = Request.Headers.ToDictionary(x => x.Key, x => x.Value.ToString());
        var inboxRequest = new PublicInboxRequest
        {
            Id = Guid.NewGuid().ToString("n"),
            InboxToken = token,
            Method = Request.Method,
            Headers = headers,
            Body = body,
            CreatedAt = now,
            ReceivedAt = now,
        };

        inbox.RequestCount++;
        inbox.UpdatedAt = now;

        await requestRepository.AddAsync(inboxRequest, cancellationToken);
        await inboxRepository.UpdateAsync(inbox, cancellationToken);

        return Ok(new { message = "Webhook received." });
    }

    [HttpGet("public/inbox/{token}")]
    public async Task<ActionResult<object>> GetAsync(string token, CancellationToken cancellationToken)
    {
        var inbox = await inboxRepository.FirstOrDefaultAsync(x => x.Token == token, cancellationToken);
        if (inbox is null)
        {
            return NotFound();
        }

        var requests = await requestRepository.FindAsync(x => x.InboxToken == token, cancellationToken);
        var ordered = requests.OrderByDescending(x => x.ReceivedAt)
            .Select(x => new { method = x.Method, headers = x.Headers, body = x.Body, receivedAt = x.ReceivedAt });

        return Ok(new
        {
            token = inbox.Token,
            expiresAt = inbox.ExpiresAt,
            maxRequests = inbox.MaxRequests,
            requestCount = inbox.RequestCount,
            remainingRequests = Math.Max(0, inbox.MaxRequests - inbox.RequestCount),
            isExpired = inbox.ExpiresAt <= dateTimeProvider.UtcNow,
            requests = ordered,
        });
    }
}
