namespace JobScout.Core.DTOs;

public class SetupStatusDto
{
    public bool NeedsSetup { get; set; }
}

public class CompleteSetupRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public string? AnthropicApiKey { get; set; }
    public string? SerpApiKey { get; set; }
    public string? AdzunaAppId { get; set; }
    public string? AdzunaAppKey { get; set; }

    public string? FirstProfileName { get; set; }
}
