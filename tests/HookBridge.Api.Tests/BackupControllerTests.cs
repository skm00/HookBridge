using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FluentValidation;
using HookBridge.Api.Controllers;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace HookBridge.Api.Tests;

public sealed class BackupControllerTests
{
    [Fact]
    public async Task Export_ReturnsFile()
    {
        var service = new FakeBackupService();
        var controller = new BackupController(service, TenantIsolationTestHelpers.CreateValidator());

        var result = await controller.ExportAsync("tenant-1", CancellationToken.None);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/gzip", fileResult.ContentType);
        Assert.Equal("tenant-1", service.LastExportTenantId);
    }

    [Fact]
    public async Task Restore_BlocksLargeFile()
    {
        var service = new FakeBackupService();
        var controller = new BackupController(service, TenantIsolationTestHelpers.CreateValidator());

        await using var ms = new MemoryStream(new byte[10 * 1024 * 1024 + 1]);
        var file = new FormFile(ms, 0, ms.Length, "file", "large.json.gz");

        await Assert.ThrowsAsync<ValidationException>(() => controller.ImportAsync("tenant-1", file, CancellationToken.None));
    }

    [Fact]
    public async Task Restore_ReturnsSummary()
    {
        var service = new FakeBackupService();
        var controller = new BackupController(service, TenantIsolationTestHelpers.CreateValidator());
        var file = BuildFile("tenant-1");

        var result = await controller.ImportAsync("tenant-1", file, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(ok.Value);
        Assert.Equal("tenant-1", service.LastImportTenantId);
    }

    private static IFormFile BuildFile(string tenantId)
    {
        var package = new TenantBackupPackage
        {
            TenantId = tenantId,
            Tenant = new HookBridge.Domain.Entities.Tenant { Id = tenantId, Name = "Acme", Slug = "acme" },
        };

        var json = JsonSerializer.Serialize(package, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var raw = Encoding.UTF8.GetBytes(json);
        var gz = new MemoryStream();
        using (var gzip = new GZipStream(gz, CompressionLevel.SmallestSize, true))
        {
            gzip.Write(raw, 0, raw.Length);
        }

        gz.Position = 0;
        return new FormFile(gz, 0, gz.Length, "file", "backup.json.gz");
    }

    private sealed class FakeBackupService : IBackupService
    {
        public string? LastExportTenantId { get; private set; }

        public string? LastImportTenantId { get; private set; }

        public Task<byte[]> ExportAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            LastExportTenantId = tenantId;
            return Task.FromResult(new byte[] { 0x1f, 0x8b, 0x08, 0x00 });
        }

        public Task ImportAsync(string tenantId, byte[] data, CancellationToken cancellationToken = default)
        {
            LastImportTenantId = tenantId;
            return Task.CompletedTask;
        }
    }
}
