using JobScout.Core.Enums;
using JobScout.Core.Models;

namespace JobScout.Infrastructure.Tests.Builders;

public class ProfileBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Test Profile";
    private string _userId = "test-user";
    private string? _resumeText = "Experienced developer with Python, React, AWS.";
    private string? _description = "A test profile.";
    private List<string> _searchKeywords = ["software", "developer"];
    private List<JobSource> _preferredSources = [];
    private string? _preferredModel;
    private decimal? _desiredSalaryMin;
    private decimal? _desiredSalaryMax;
    private bool _isActive = true;

    public ProfileBuilder WithId(Guid id) { _id = id; return this; }
    public ProfileBuilder WithName(string name) { _name = name; return this; }
    public ProfileBuilder WithUserId(string userId) { _userId = userId; return this; }
    public ProfileBuilder WithResumeText(string? text) { _resumeText = text; return this; }
    public ProfileBuilder WithSearchKeywords(params string[] keywords) { _searchKeywords = [.. keywords]; return this; }
    public ProfileBuilder WithPreferredSources(params JobSource[] sources) { _preferredSources = [.. sources]; return this; }
    public ProfileBuilder WithPreferredModel(string model) { _preferredModel = model; return this; }
    public ProfileBuilder WithSalaryRange(decimal? min, decimal? max)
    {
        _desiredSalaryMin = min;
        _desiredSalaryMax = max;
        return this;
    }

    public SearchProfile Build() => new()
    {
        Id = _id,
        Name = _name,
        UserId = _userId,
        ResumeText = _resumeText,
        Description = _description,
        SearchKeywords = _searchKeywords,
        PreferredSources = _preferredSources,
        PreferredModel = _preferredModel,
        DesiredSalaryMin = _desiredSalaryMin,
        DesiredSalaryMax = _desiredSalaryMax,
        IsActive = _isActive,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
