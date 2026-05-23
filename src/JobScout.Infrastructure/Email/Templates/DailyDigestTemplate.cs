using System.Text;
using JobScout.Core.Models;

namespace JobScout.Infrastructure.Email.Templates;

public record DigestJob(Job Job, decimal Score);

public static class DailyDigestTemplate
{
    public static EmailTemplateResult Render(string displayName, IReadOnlyList<DigestJob> jobs, string appBaseUrl)
    {
        var subject = $"Your JobScout digest — {jobs.Count} new match{(jobs.Count == 1 ? "" : "es")}";

        var html = new StringBuilder();
        html.Append("""
            <!DOCTYPE html><html><body style="margin:0;padding:0;font-family:-apple-system,Segoe UI,Helvetica,Arial,sans-serif;background:#f5f6f8;color:#1a1c20;">
              <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#f5f6f8;padding:24px 0;">
                <tr><td align="center">
                  <table width="600" cellpadding="0" cellspacing="0" border="0" style="background:#ffffff;border-radius:12px;border:1px solid #e3e5ea;">
                    <tr><td style="padding:24px 28px 16px;border-bottom:1px solid #e3e5ea;">
            """);
        html.Append($"<h1 style=\"font-size:20px;margin:0;\">Good morning, {Escape(displayName)}</h1>");
        html.Append($"<p style=\"font-size:14px;color:#5a5d66;margin:6px 0 0;\">{jobs.Count} strong fit{(jobs.Count == 1 ? "" : "s")} from the last 24 hours.</p>");
        html.Append("</td></tr>");

        foreach (var entry in jobs)
        {
            var j = entry.Job;
            html.Append("<tr><td style=\"padding:16px 28px;border-bottom:1px solid #f0f1f4;\">");
            html.Append("<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr>");
            html.Append("<td style=\"vertical-align:top;\">");
            html.Append($"<div style=\"font-weight:600;font-size:15px;\"><a href=\"{Escape(appBaseUrl)}/?jobId={j.Id}\" style=\"color:#1a1c20;text-decoration:none;\">{Escape(j.Title)}</a></div>");
            html.Append($"<div style=\"font-size:13px;color:#5a5d66;\">{Escape(j.Company)} · {Escape(j.Location)} · {j.Source}</div>");
            html.Append("</td>");
            html.Append($"<td align=\"right\" style=\"width:60px;vertical-align:top;\"><div style=\"font-size:22px;font-weight:700;color:#22c55e;\">{entry.Score:F1}</div></td>");
            html.Append("</tr></table></td></tr>");
        }

        html.Append("<tr><td style=\"padding:20px 28px 28px;text-align:center;\">");
        html.Append($"<a href=\"{Escape(appBaseUrl)}/strong-fit\" style=\"display:inline-block;background:#6aa6ff;color:#ffffff;text-decoration:none;padding:10px 22px;border-radius:8px;font-weight:600;font-size:14px;\">Open JobScout</a>");
        html.Append("</td></tr></table>");
        html.Append("<div style=\"font-size:11px;color:#9a9da8;margin-top:16px;\">Adjust digest cadence in JobScout → Settings.</div>");
        html.Append("</td></tr></table></body></html>");

        var text = new StringBuilder();
        text.AppendLine($"JobScout Digest — {jobs.Count} strong fit(s) in the last 24 hours");
        text.AppendLine();
        foreach (var entry in jobs)
        {
            text.AppendLine($"• {entry.Job.Title} @ {entry.Job.Company} ({entry.Job.Location}) — {entry.Score:F1}/10");
            text.AppendLine($"  {appBaseUrl}/?jobId={entry.Job.Id}");
        }
        text.AppendLine();
        text.AppendLine($"Open JobScout: {appBaseUrl}/strong-fit");

        return new EmailTemplateResult(subject, html.ToString(), text.ToString());
    }

    private static string Escape(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
}
