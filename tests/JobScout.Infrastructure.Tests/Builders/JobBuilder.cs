using JobScout.Core.Enums;
using JobScout.Core.Models;

namespace JobScout.Infrastructure.Tests.Builders;

public class JobBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _externalId = $"ext-{Guid.NewGuid():N}";
    private string _title = "Senior Software Engineer";
    private string _company = "Acme Corp";
    private string? _location = "Remote";
    private LocationType _locationType = LocationType.Remote;
    private JobType _jobType = JobType.FullTime;
    private string _description = "Build great software.";
    private string? _salary;
    private DateTime? _postedAt = DateTime.UtcNow.AddDays(-1);
    private DateTime _discoveredAt = DateTime.UtcNow;
    private JobSource _source = JobSource.RemoteOK;
    private string _sourceUrl = "https://example.com/job/1";
    private bool _isActive = true;

    public JobBuilder WithId(Guid id) { _id = id; return this; }
    public JobBuilder WithExternalId(string id) { _externalId = id; return this; }
    public JobBuilder WithTitle(string title) { _title = title; return this; }
    public JobBuilder WithCompany(string company) { _company = company; return this; }
    public JobBuilder WithLocation(string? location) { _location = location; return this; }
    public JobBuilder WithSource(JobSource source) { _source = source; return this; }
    public JobBuilder WithSourceUrl(string url) { _sourceUrl = url; return this; }
    public JobBuilder WithSalary(string salary) { _salary = salary; return this; }
    public JobBuilder WithDescription(string description) { _description = description; return this; }
    public JobBuilder WithPostedAt(DateTime postedAt) { _postedAt = postedAt; return this; }
    public JobBuilder InactiveJob() { _isActive = false; return this; }

    public Job Build() => new()
    {
        Id = _id,
        ExternalId = _externalId,
        Title = _title,
        Company = _company,
        Location = _location,
        LocationType = _locationType,
        JobType = _jobType,
        Description = _description,
        Salary = _salary,
        PostedAt = _postedAt,
        DiscoveredAt = _discoveredAt,
        Source = _source,
        SourceUrl = _sourceUrl,
        IsActive = _isActive
    };
}
