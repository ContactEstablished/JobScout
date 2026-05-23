using JobScout.Core.Models;

namespace JobScout.Core.Interfaces;

public interface IDeduplicationService
{
    string NormalizeTitle(string title);
    string NormalizeCompany(string company);
    Task<Job?> FindFuzzyDuplicateAsync(Job candidate);
}
