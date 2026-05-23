namespace JobScout.Core.Tests.Models;

public class JobTests
{
    [Fact]
    public void NewJob_DefaultsAreSensible()
    {
        var job = new Job();

        job.Title.Should().BeEmpty();
        job.Company.Should().BeEmpty();
        job.Description.Should().BeEmpty();
        job.Tags.Should().Be("[]");
        job.SourceUrl.Should().BeEmpty();
        job.IsActive.Should().BeTrue();
        job.IsPotentialDuplicate.Should().BeFalse();
        job.DuplicateOfJobId.Should().BeNull();
        job.AlternateSourceUrls.Should().BeEmpty();
        job.AiScores.Should().BeEmpty();
    }

    [Fact]
    public void NewSearchProfile_DefaultsAreSensible()
    {
        var profile = new SearchProfile();

        profile.SearchKeywords.Should().BeEmpty();
        profile.PreferredSources.Should().BeEmpty();
        profile.PreferredJobTypes.Should().BeEmpty();
        profile.DetectedSkills.Should().BeEmpty();
        profile.IsActive.Should().BeFalse();
        profile.PreferredModel.Should().BeNull();
        profile.DesiredSalaryMin.Should().BeNull();
        profile.DesiredSalaryMax.Should().BeNull();
    }

    [Fact]
    public void NewAiScore_DefaultsAreSensible()
    {
        var score = new AiScore();

        score.Score.Should().Be(0m);
        score.MatchedKeywords.Should().Be("[]");
        score.GrowthAreas.Should().Be("[]");
        score.RedFlags.Should().Be("[]");
        score.SkillsMatchScore.Should().BeNull();
        score.ExperienceFitScore.Should().BeNull();
        score.CultureFitScore.Should().BeNull();
        score.CompensationFitScore.Should().BeNull();
        score.InputTokens.Should().BeNull();
        score.OutputTokens.Should().BeNull();
        score.EstimatedCostUsd.Should().BeNull();
    }

    [Fact]
    public void NewNotification_DefaultsToUnread()
    {
        var n = new Notification();

        n.IsRead.Should().BeFalse();
        n.ReadAt.Should().BeNull();
        n.RelatedJobId.Should().BeNull();
        n.RelatedApplicationId.Should().BeNull();
    }

    [Fact]
    public void NewNotificationPreferences_HasInAppDefaultsOnAndEmailOff()
    {
        var p = new NotificationPreferences();

        p.InAppNewStrongFit.Should().BeTrue();
        p.InAppScoreUpdate.Should().BeTrue();
        p.InAppIngestionComplete.Should().BeTrue();
        p.InAppApplicationStatusChange.Should().BeTrue();
        p.EmailDailyDigest.Should().BeFalse();
        p.EmailWeeklySummary.Should().BeFalse();
        p.EmailInstantStrongMatch.Should().BeFalse();
        p.TimeZoneId.Should().Be("UTC");
    }
}
