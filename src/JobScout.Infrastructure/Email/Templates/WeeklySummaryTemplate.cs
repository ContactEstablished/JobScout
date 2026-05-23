using System.Text;

namespace JobScout.Infrastructure.Email.Templates;

public record WeeklySummaryData(
    string DisplayName,
    int TotalJobs,
    int StrongFits,
    int Applied,
    int Interviewing,
    int Offered,
    IReadOnlyList<DigestJob> TopJobs);

public static class WeeklySummaryTemplate
{
    public static EmailTemplateResult Render(WeeklySummaryData data, string appBaseUrl)
    {
        var subject = $"JobScout: {data.TotalJobs} jobs, {data.StrongFits} strong fits this week";

        var html = new StringBuilder();
        html.Append("""
            <!DOCTYPE html><html><body style="margin:0;padding:0;font-family:-apple-system,Segoe UI,Helvetica,Arial,sans-serif;background:#f5f6f8;color:#1a1c20;">
              <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#f5f6f8;padding:24px 0;">
                <tr><td align="center">
                  <table width="600" cellpadding="0" cellspacing="0" border="0" style="background:#ffffff;border-radius:12px;border:1px solid #e3e5ea;">
                    <tr><td style="padding:24px 28px 16px;border-bottom:1px solid #e3e5ea;">
            """);
        html.Append($"<h1 style=\"font-size:20px;margin:0;\">This week in JobScout</h1>");
        html.Append($"<p style=\"font-size:14px;color:#5a5d66;margin:6px 0 0;\">Hi {Escape(data.DisplayName)}, here's the rollup.</p>");
        html.Append("</td></tr>");

        html.Append("<tr><td style=\"padding:20px 28px;\">");
        html.Append("<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr>");
        html.Append(StatCell("New jobs", data.TotalJobs));
        html.Append(StatCell("Strong fits", data.StrongFits));
        html.Append(StatCell("Applied", data.Applied));
        html.Append(StatCell("Interviewing", data.Interviewing));
        html.Append(StatCell("Offered", data.Offered));
        html.Append("</tr></table>");
        html.Append("</td></tr>");

        if (data.TopJobs.Count > 0)
        {
            html.Append("<tr><td style=\"padding:8px 28px 0;font-weight:600;font-size:14px;color:#5a5d66;\">Top 5 jobs of the week</td></tr>");
            foreach (var entry in data.TopJobs)
            {
                var j = entry.Job;
                html.Append("<tr><td style=\"padding:12px 28px;border-bottom:1px solid #f0f1f4;\">");
                html.Append($"<div style=\"font-weight:600;font-size:14px;\"><a href=\"{Escape(appBaseUrl)}/?jobId={j.Id}\" style=\"color:#1a1c20;text-decoration:none;\">{Escape(j.Title)}</a></div>");
                html.Append($"<div style=\"font-size:12px;color:#5a5d66;\">{Escape(j.Company)} — {entry.Score:F1}/10</div>");
                html.Append("</td></tr>");
            }
        }

        html.Append("<tr><td style=\"padding:20px 28px 28px;text-align:center;\">");
        html.Append($"<a href=\"{Escape(appBaseUrl)}/trends\" style=\"display:inline-block;background:#6aa6ff;color:#ffffff;text-decoration:none;padding:10px 22px;border-radius:8px;font-weight:600;font-size:14px;\">See full trends</a>");
        html.Append("</td></tr></table></td></tr></table></body></html>");

        var text = new StringBuilder();
        text.AppendLine($"This week in JobScout — {data.DisplayName}");
        text.AppendLine();
        text.AppendLine($"New jobs:     {data.TotalJobs}");
        text.AppendLine($"Strong fits:  {data.StrongFits}");
        text.AppendLine($"Applied:      {data.Applied}");
        text.AppendLine($"Interviewing: {data.Interviewing}");
        text.AppendLine($"Offered:      {data.Offered}");
        text.AppendLine();
        if (data.TopJobs.Count > 0)
        {
            text.AppendLine("Top jobs:");
            foreach (var entry in data.TopJobs)
                text.AppendLine($"• {entry.Job.Title} @ {entry.Job.Company} — {entry.Score:F1}/10");
            text.AppendLine();
        }
        text.AppendLine($"See full trends: {appBaseUrl}/trends");

        return new EmailTemplateResult(subject, html.ToString(), text.ToString());
    }

    private static string StatCell(string label, int value)
        => $"<td align=\"center\" style=\"padding:6px;\">"
         + $"<div style=\"font-size:22px;font-weight:700;color:#1a1c20;\">{value}</div>"
         + $"<div style=\"font-size:11px;color:#9a9da8;text-transform:uppercase;letter-spacing:0.06em;\">{Escape(label)}</div>"
         + "</td>";

    private static string Escape(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
}
