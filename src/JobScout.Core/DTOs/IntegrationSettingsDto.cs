namespace JobScout.Core.DTOs;

/// <summary>
/// Integration credentials surfaced via /api/settings/integrations.
/// GET returns masked values (last 4 chars). PUT writes new values; empty string clears.
/// </summary>
public class IntegrationSettingsDto
{
    public string? AnthropicApiKey { get; set; }
    public string? SerpApiKey { get; set; }
    public string? AdzunaAppId { get; set; }
    public string? AdzunaAppKey { get; set; }
    public string? SendGridApiKey { get; set; }
    public string? SendGridFromAddress { get; set; }
    public string? WellfoundAccessToken { get; set; }
}
