using JobScout.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace JobScout.Infrastructure.Email;

public class NullEmailSender(ILogger<NullEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        logger.LogInformation(
            "Email suppressed (no SendGrid key) — To: {To}, Subject: {Subject}",
            message.ToAddress, message.Subject);
        return Task.CompletedTask;
    }
}
