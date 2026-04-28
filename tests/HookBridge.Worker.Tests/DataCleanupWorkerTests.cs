using HookBridge.Application.Interfaces.Services;
using HookBridge.Infrastructure.Configuration;
using HookBridge.Worker;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace HookBridge.Worker.Tests;

public sealed class DataCleanupWorkerTests
{
    [Fact]
    public async Task RunCleanupCycleAsync_SkipsWhenDisabled()
    {
        var cleanupMock = new Mock<IDataCleanupService>(MockBehavior.Strict);
        var logger = new TestLogger<DataCleanupWorker>();
        var worker = new DataCleanupWorker(cleanupMock.Object, Options.Create(new DataRetentionSettings { Enabled = false }), logger);

        await worker.RunCleanupCycleAsync(CancellationToken.None);

        cleanupMock.VerifyNoOtherCalls();
        Assert.Contains(logger.Records, r => r.Message.Contains("disabled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunCleanupCycleAsync_RespectsRetentionSettings()
    {
        var cleanupMock = new Mock<IDataCleanupService>();
        cleanupMock.Setup(x => x.CleanupIncomingEventsAsync(11, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        cleanupMock.Setup(x => x.CleanupDeliveryLogsAsync(12, It.IsAny<CancellationToken>())).ReturnsAsync(2);
        cleanupMock.Setup(x => x.CleanupFailedEventsAsync(13, It.IsAny<CancellationToken>())).ReturnsAsync(3);
        cleanupMock.Setup(x => x.CleanupAuditLogsAsync(14, It.IsAny<CancellationToken>())).ReturnsAsync(4);
        cleanupMock.Setup(x => x.CleanupNotificationsAsync(15, It.IsAny<CancellationToken>())).ReturnsAsync(5);

        var worker = new DataCleanupWorker(
            cleanupMock.Object,
            Options.Create(new DataRetentionSettings
            {
                Enabled = true,
                IncomingEventsDays = 11,
                DeliveryLogsDays = 12,
                FailedEventsDays = 13,
                AuditLogsDays = 14,
                NotificationsDays = 15,
            }),
            new TestLogger<DataCleanupWorker>());

        await worker.RunCleanupCycleAsync(CancellationToken.None);

        cleanupMock.Verify(x => x.CleanupIncomingEventsAsync(11, It.IsAny<CancellationToken>()), Times.Once);
        cleanupMock.Verify(x => x.CleanupDeliveryLogsAsync(12, It.IsAny<CancellationToken>()), Times.Once);
        cleanupMock.Verify(x => x.CleanupFailedEventsAsync(13, It.IsAny<CancellationToken>()), Times.Once);
        cleanupMock.Verify(x => x.CleanupAuditLogsAsync(14, It.IsAny<CancellationToken>()), Times.Once);
        cleanupMock.Verify(x => x.CleanupNotificationsAsync(15, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunCleanupCycleAsync_TriggersAllCleanupMethods()
    {
        var cleanupMock = new Mock<IDataCleanupService>();
        cleanupMock.Setup(x => x.CleanupIncomingEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        cleanupMock.Setup(x => x.CleanupDeliveryLogsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        cleanupMock.Setup(x => x.CleanupFailedEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        cleanupMock.Setup(x => x.CleanupAuditLogsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        cleanupMock.Setup(x => x.CleanupNotificationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var worker = new DataCleanupWorker(cleanupMock.Object, Options.Create(new DataRetentionSettings { Enabled = true }), new TestLogger<DataCleanupWorker>());

        await worker.RunCleanupCycleAsync(CancellationToken.None);

        cleanupMock.Verify(x => x.CleanupIncomingEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        cleanupMock.Verify(x => x.CleanupDeliveryLogsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        cleanupMock.Verify(x => x.CleanupFailedEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        cleanupMock.Verify(x => x.CleanupAuditLogsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        cleanupMock.Verify(x => x.CleanupNotificationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
