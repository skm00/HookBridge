using HookBridge.Application.DTOs.Common;
using HookBridge.Application.DTOs.Notifications;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Constants;
using HookBridge.Domain.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace HookBridge.Application.Services;

public sealed class NotificationService(
    INotificationRepository notificationRepository,
    IGuidGenerator guidGenerator,
    IDateTimeProvider dateTimeProvider,
    IMongoRepository<Tenant> tenantRepository,
    IEmailSender emailSender,
    IFeatureFlagService featureFlagService,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task CreateAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        notification.Id = string.IsNullOrWhiteSpace(notification.Id) ? guidGenerator.NewGuid() : notification.Id;
        notification.CreatedAt = notification.CreatedAt == default ? dateTimeProvider.UtcNow : notification.CreatedAt;
        notification.UpdatedAt = null;

        await notificationRepository.AddAsync(notification, cancellationToken);
        await TrySendEmailAsync(notification, cancellationToken);
    }

    public async Task<PagedResponseDto<NotificationResponseDto>> SearchAsync(
        NotificationSearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        var sort = Builders<Notification>.Sort.Descending(x => x.CreatedAt);

        var result = await notificationRepository.SearchAsync(request, sort, request.Skip, pageSize, cancellationToken);
        return PagedResponseDto<NotificationResponseDto>.Create(result.Items.Select(Map).ToList(), pageNumber, pageSize, result.TotalCount);
    }

    public async Task<NotificationResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var notification = await notificationRepository.GetByIdAsync(id, cancellationToken);
        return notification is null ? null : Map(notification);
    }

    public async Task<bool> MarkAsReadAsync(string id, CancellationToken cancellationToken = default)
    {
        var notification = await notificationRepository.GetByIdAsync(id, cancellationToken);
        if (notification is null)
        {
            return false;
        }

        if (notification.IsRead)
        {
            return true;
        }

        var now = dateTimeProvider.UtcNow;
        notification.IsRead = true;
        notification.ReadAt = now;
        notification.UpdatedAt = now;
        await notificationRepository.UpdateAsync(notification, cancellationToken);
        return true;
    }

    public Task<int> GetUnreadCountAsync(string tenantId, CancellationToken cancellationToken = default)
        => notificationRepository.GetUnreadCountAsync(tenantId, cancellationToken);

    private async Task TrySendEmailAsync(Notification notification, CancellationToken cancellationToken)
    {
        if (!featureFlagService.IsEnabled("EnableEmailNotifications", notification.TenantId))
        {
            return;
        }

        if (notification.Severity is not (NotificationSeverities.Critical or NotificationSeverities.Error))
        {
            return;
        }

        try
        {
            var tenant = await tenantRepository.GetByIdAsync(notification.TenantId, cancellationToken);
            if (tenant is null)
            {
                logger.LogWarning("Tenant {TenantId} not found for notification email dispatch.", notification.TenantId);
                return;
            }

            var recipients = (tenant.NotificationEmails ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (recipients.Count == 0 && !string.IsNullOrWhiteSpace(tenant.ContactEmail))
            {
                recipients.Add(tenant.ContactEmail);
            }

            if (recipients.Count == 0)
            {
                logger.LogInformation(
                    "No notification recipients configured for tenant {TenantId}; skipping notification email.",
                    tenant.Id);
                return;
            }

            var subject = $"[{notification.Severity}] {notification.Title}";
            var htmlBody = BuildHtmlBody(notification);
            foreach (var recipient in recipients)
            {
                await emailSender.SendAsync(recipient, subject, htmlBody, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send notification email for notification {NotificationId}.", notification.Id);
        }
    }

    private static string BuildHtmlBody(Notification notification)
    {
        return $"""
                <html>
                  <body>
                    <h2>{notification.Title}</h2>
                    <p><strong>Severity:</strong> {notification.Severity}</p>
                    <p><strong>Message:</strong> {notification.Message}</p>
                    <p><strong>ResourceType:</strong> {notification.ResourceType ?? "-"}</p>
                    <p><strong>ResourceId:</strong> {notification.ResourceId ?? "-"}</p>
                    <p><strong>CreatedAt:</strong> {notification.CreatedAt:O}</p>
                    <p><a href="{{DASHBOARD_NOTIFICATION_URL}}">Open notification in dashboard</a></p>
                  </body>
                </html>
                """;
    }

    private static NotificationResponseDto Map(Notification notification) => new()
    {
        Id = notification.Id,
        TenantId = notification.TenantId,
        Type = notification.Type,
        Severity = notification.Severity,
        Title = notification.Title,
        Message = notification.Message,
        ResourceType = notification.ResourceType,
        ResourceId = notification.ResourceId,
        IsRead = notification.IsRead,
        CreatedAt = notification.CreatedAt,
        ReadAt = notification.ReadAt,
    };
}
