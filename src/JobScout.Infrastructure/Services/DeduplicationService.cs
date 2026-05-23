using System.Text.RegularExpressions;
using JobScout.Core.Enums;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;

namespace JobScout.Infrastructure.Services;

public partial class DeduplicationService(IJobRepository jobs) : IDeduplicationService
{
    private static readonly string[] TitleStopwords =
        ["sr", "senior", "jr", "junior", "lead", "staff", "principal", "associate", "remote"];

    private static readonly string[] CompanySuffixes =
        ["inc", "llc", "ltd", "corp", "co", "company", "incorporated", "limited", "technologies", "solutions", "group"];

    public string NormalizeTitle(string title)
    {
        var s = title.ToLowerInvariant();
        s = NonAlphanumericRegex().Replace(s, " ");
        var words = s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                     .Where(w => !TitleStopwords.Contains(w));
        return string.Join(" ", words).Trim();
    }

    public string NormalizeCompany(string company)
    {
        var s = company.ToLowerInvariant();
        s = NonAlphanumericRegex().Replace(s, " ");
        var words = s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                     .Where(w => !CompanySuffixes.Contains(w));
        return string.Join(" ", words).Trim();
    }

    public async Task<Job?> FindFuzzyDuplicateAsync(Job candidate)
    {
        var normTitle   = NormalizeTitle(candidate.Title);
        var normCompany = NormalizeCompany(candidate.Company);
        return await jobs.FindFuzzyDuplicateAsync(normTitle, normCompany, candidate.Source);
    }

    [GeneratedRegex(@"[^a-z0-9\s]")]
    private static partial Regex NonAlphanumericRegex();
}
