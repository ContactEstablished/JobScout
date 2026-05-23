using JobScout.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace JobScout.Infrastructure.Email;

public class SendGridEmailSender : IEmailSender
{
    private readonly SendGridClient _client;
    private readonly EmailAddress _from;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(IConfiguration config, ILogger<SendGridEmailSender> logger)
    {
        var apiKey = config["SendGrid:ApiKey"]
            ?? throw new InvalidOperationException("SendGrid:ApiKey is not configured.");
        var fromAddress = config["SendGrid:FromAddress"] ?? "no-reply@jobscout.local";
        var fromName = config["SendGrid:FromName"] ?? "JobScout";

        _client = new SendGridClient(apiKey);
        _from = new EmailAddress(fromAddress, fromName);
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var to = new EmailAddress(message.ToAddress, message.ToName);
        var mail = MailHelper.CreateSingleEmail(
            _from, to, message.Subject, message.PlainTextBody, message.HtmlBody);

        var response = await _client.SendEmailAsync(mail, ct);
        if (!IsSuccess(response.StatusCode))
        {
            var body = await response.Body.ReadAsStringAsync(ct);
            _logger.LogError(
                "SendGrid returned {Status} when sending to {To}: {Body}",
                (int)response.StatusCode, message.ToAddress, body);
            throw new InvalidOperationException(
                $"SendGrid rejected the email (HTTP {(int)response.StatusCode}).");
        }

        _logger.LogInformation("Email sent to {To} via SendGrid: {Subject}",
            message.ToAddress, message.Subject);
    }

    private static bool IsSuccess(System.Net.HttpStatusCode status)
        => (int)status >= 200 && (int)status < 300;
}
