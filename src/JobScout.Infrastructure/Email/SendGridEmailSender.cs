using JobScout.Core.Interfaces;
using JobScout.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace JobScout.Infrastructure.Email;

/// <summary>
/// Sends mail via SendGrid when an API key is present in the secret store; otherwise logs
/// the suppressed message and returns. No-op behavior makes the app safe to run with no
/// integrations configured — the user can enable email later from the Settings page.
/// </summary>
public class SendGridEmailSender : IEmailSender
{
    private readonly ISecretStore _secrets;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(ISecretStore secrets, ILogger<SendGridEmailSender> logger)
    {
        _secrets = secrets;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var apiKey = await _secrets.GetAsync("SendGrid:ApiKey", ct);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogInformation(
                "Email suppressed (no SendGrid key) — To: {To}, Subject: {Subject}",
                message.ToAddress, message.Subject);
            return;
        }

        var fromAddress = await _secrets.GetAsync("SendGrid:FromAddress", ct) ?? "no-reply@jobscout.local";
        var fromName = await _secrets.GetAsync("SendGrid:FromName", ct) ?? "JobScout";

        var client = new SendGridClient(apiKey);
        var from = new EmailAddress(fromAddress, fromName);
        var to = new EmailAddress(message.ToAddress, message.ToName);
        var mail = MailHelper.CreateSingleEmail(
            from, to, message.Subject, message.PlainTextBody, message.HtmlBody);

        var response = await client.SendEmailAsync(mail, ct);
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
