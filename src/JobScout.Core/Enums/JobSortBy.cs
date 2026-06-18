namespace JobScout.Core.Enums;

public enum JobSortBy
{
    AiScore,      // default — the profile's AI fit score, descending
    PostedDate,   // PostedAt (falls back to DiscoveredAt), descending
    Company       // company name, ascending
}
