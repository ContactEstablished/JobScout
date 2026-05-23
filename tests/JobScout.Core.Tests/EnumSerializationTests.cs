namespace JobScout.Core.Tests;

public class EnumSerializationTests
{
    [Theory]
    [InlineData(JobSource.LinkedIn, "LinkedIn")]
    [InlineData(JobSource.Indeed, "Indeed")]
    [InlineData(JobSource.Glassdoor, "Glassdoor")]
    [InlineData(JobSource.Dice, "Dice")]
    [InlineData(JobSource.Wellfound, "Wellfound")]
    [InlineData(JobSource.RemoteOK, "RemoteOK")]
    [InlineData(JobSource.Adzuna, "Adzuna")]
    [InlineData(JobSource.TheMuse, "TheMuse")]
    [InlineData(JobSource.Custom, "Custom")]
    public void JobSource_RoundTrips(JobSource source, string expected)
    {
        source.ToString().Should().Be(expected);
        Enum.Parse<JobSource>(expected).Should().Be(source);
    }

    [Theory]
    [InlineData(ApplicationStatus.Applied)]
    [InlineData(ApplicationStatus.Interviewing)]
    [InlineData(ApplicationStatus.Offered)]
    [InlineData(ApplicationStatus.Accepted)]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Withdrawn)]
    public void ApplicationStatus_RoundTrips(ApplicationStatus status)
    {
        Enum.Parse<ApplicationStatus>(status.ToString()).Should().Be(status);
    }

    [Theory]
    [InlineData(NotificationType.NewStrongFit)]
    [InlineData(NotificationType.ScoreUpdate)]
    [InlineData(NotificationType.IngestionComplete)]
    [InlineData(NotificationType.ApplicationStatusChange)]
    public void NotificationType_RoundTrips(NotificationType type)
    {
        Enum.Parse<NotificationType>(type.ToString()).Should().Be(type);
    }

    [Theory]
    [InlineData(FeedFormat.Rss)]
    [InlineData(FeedFormat.Atom)]
    [InlineData(FeedFormat.Json)]
    public void FeedFormat_RoundTrips(FeedFormat fmt)
    {
        Enum.Parse<FeedFormat>(fmt.ToString()).Should().Be(fmt);
    }

    [Fact]
    public void JobSource_HasNineValues()
    {
        Enum.GetValues<JobSource>().Should().HaveCount(9);
    }

    [Fact]
    public void NotificationType_HasFourValues()
    {
        Enum.GetValues<NotificationType>().Should().HaveCount(4);
    }
}
