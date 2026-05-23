using System.Text;
using JobScout.Core.Models;

namespace JobScout.Infrastructure.Email.Templates;

public static class InstantAlertTemplate
{
    public static EmailTemplateResult Render(Job job, AiScore score, SearchProfile profile, string appBaseUrl)
    {
        var subject = $"New strong match: {job.Title} at {job.Company}";

        var html = new StringBuilder();
        html.Append("""
            <!DOCTYPE html><html><body style="margin:0;padding:0;font-family:-apple-system,Segoe UI,Helvetica,Arial,sans-serif;background:#f5f6f8;color:#1a1c20;">
              <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#f5f6f8;padding:24px 0;">
                <tr><td align="center">
                  <table width="560" cellpadding="0" cellspacing="0" border="0" style="background:#ffffff;border-radius:12px;border:1px solid #e3e5ea;">
                    <tr><td style="padding:24px 28px 8px;">
                      <div style="font-size:12px;letter-spacing:0.08em;text-transform:uppercase;color:#6aa6ff;font-weight:600;">★ Strong match</div>
            """);
        html.Append($"<h1 style=\"font-size:20px;margin:6px 0 2px;\">{Escape(job.Title)}</h1>");
        html.Append($"<div style=\"font-size:14px;color:#5a5d66;\">{Escape(job.Company)} · {Escape(job.Location)}</div>");
        html.Append("</td></tr><tr><td style=\"padding:8px 28px;\">");
        html.Append($"<div style=\"font-size:36px;font-weight:700;color:#22c55e;line-height:1;\">{score.Score:F1}<span style=\"font-size:18px;color:#9a9da8;font-weight:400;\">/10</span></div>");
        html.Append($"<div style=\"font-size:13px;color:#5a5d66;margin-top:4px;\">Profile: <strong>{Escape(profile.Name)}</strong></div>");
        if (!string.IsNullOrWhiteSpace(score.Reasoning))
            html.Append($"<p style=\"font-size:14px;line-height:1.5;margin:16px 0 0;color:#2a2c33;\">{Escape(score.Reasoning)}</p>");
        html.Append("</td></tr><tr><td style=\"padding:20px 28px 28px;\">");
        if (!string.IsNullOrEmpty(job.SourceUrl))
            html.Append($"<a href=\"{Escape(job.SourceUrl)}\" style=\"display:inline-block;background:#6aa6ff;color:#ffffff;text-decoration:none;padding:10px 18px;border-radius:8px;font-weight:600;font-size:14px;\">Apply on {job.Source}</a>&nbsp;");
        html.Append($"<a href=\"{Escape(appBaseUrl)}/?jobId={job.Id}\" style=\"display:inline-block;background:#ffffff;color:#6aa6ff;text-decoration:none;padding:10px 18px;border-radius:8px;font-weight:600;font-size:14px;border:1px solid #6aa6ff;\">View in JobScout</a>");
        html.Append("</td></tr></table>");
        html.Append("<div style=\"font-size:11px;color:#9a9da8;margin-top:16px;\">You can manage these alerts in JobScout → Settings.</div>");
        html.Append("</td></tr></table></body></html>");

        var text = new StringBuilder();
        text.AppendLine($"NEW STRONG MATCH — Score {score.Score:F1}/10");
        text.AppendLine();
        text.AppendLine($"{job.Title} at {job.Company}");
        text.AppendLine($"Location: {job.Location}");
        text.AppendLine($"Profile: {profile.Name}");
        text.AppendLine();
        if (!string.IsNullOrWhiteSpace(score.Reasoning))
            text.AppendLine(score.Reasoning).AppendLine();
        if (!string.IsNullOrEmpty(job.SourceUrl))
            text.AppendLine($"Apply: {job.SourceUrl}");
        text.AppendLine($"View in JobScout: {appBaseUrl}/?jobId={job.Id}");

        return new EmailTemplateResult(subject, html.ToString(), text.ToString());
    }

    private static string Escape(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
}
