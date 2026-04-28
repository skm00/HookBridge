using HookBridge.Infrastructure.Configuration;
using HookBridge.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HookBridge.Worker.Tests;

public sealed class SmtpEmailSenderTests
{
    [Fact]
    public async Task SendAsync_EmailDisabled_SkipsWithoutFailure()
    {
        var sender = new SmtpEmailSender(
            Options.Create(new EmailSettings
            {
                Enabled = false,
                Provider = "Smtp",
                SmtpHost = "invalid.local",
                SmtpPort = 587,
                SmtpUsername = "user",
                SmtpPassword = "secret",
                FromEmail = "noreply@hookbridge.local",
                FromName = "HookBridge",
                UseSsl = true,
            }),
            NullLogger<SmtpEmailSender>.Instance);

        await sender.SendAsync("admin@tenant.com", "subject", "<p>body</p>");
    }
}
