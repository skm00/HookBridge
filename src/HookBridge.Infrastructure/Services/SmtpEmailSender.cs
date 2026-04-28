using HookBridge.Application.Interfaces;
using HookBridge.Infrastructure.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace HookBridge.Infrastructure.Services;

public sealed class SmtpEmailSender(
    IOptions<EmailSettings> settings,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var emailSettings = settings.Value;
        if (!emailSettings.Enabled)
        {
            logger.LogInformation("Email sending is disabled. Skipping email to {Email}.", toEmail);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(emailSettings.FromName, emailSettings.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder
        {
            HtmlBody = htmlBody,
        }.ToMessageBody();

        using var client = new SmtpClient();
        var secureSocketOptions = emailSettings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;

        logger.LogInformation(
            "Sending email via SMTP host {Host}:{Port} using provider {Provider}.",
            emailSettings.SmtpHost,
            emailSettings.SmtpPort,
            emailSettings.Provider);

        await client.ConnectAsync(emailSettings.SmtpHost, emailSettings.SmtpPort, secureSocketOptions, cancellationToken);
        if (!string.IsNullOrWhiteSpace(emailSettings.SmtpUsername))
        {
            await client.AuthenticateAsync(emailSettings.SmtpUsername, emailSettings.SmtpPassword, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
