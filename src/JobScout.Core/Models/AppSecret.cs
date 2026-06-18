namespace JobScout.Core.Models;

/// <summary>
/// System-wide encrypted secret store (API keys, etc.). One row per logical key.
/// </summary>
public class AppSecret
{
    /// <summary>Logical key, e.g. "Anthropic:ApiKey".</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Ciphertext produced by the ASP.NET Data Protection API.</summary>
    public string EncryptedValue { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }
}
