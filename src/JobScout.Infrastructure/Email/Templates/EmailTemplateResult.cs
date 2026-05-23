namespace JobScout.Infrastructure.Email.Templates;

public record EmailTemplateResult(string Subject, string HtmlBody, string PlainTextBody);
