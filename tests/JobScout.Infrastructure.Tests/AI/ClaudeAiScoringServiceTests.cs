using System.Text.Json.Nodes;
using Anthropic.SDK.Messaging;
using JobScout.Infrastructure.AI;
using JobScout.Infrastructure.Configuration;
using JobScout.Infrastructure.Tests.Builders;
using JobScout.Infrastructure.Tests.Fixtures;
using Microsoft.Extensions.Configuration;

namespace JobScout.Infrastructure.Tests.AI;

public class ClaudeAiScoringServiceTests
{
    private static IConfiguration BuildConfig()
        => new ConfigurationBuilder().AddInMemoryCollection().Build();

    private static ISecretStore BuildSecretStore(string? apiKey)
    {
        var store = Substitute.For<ISecretStore>();
        store.GetAsync("Anthropic:ApiKey", Arg.Any<CancellationToken>())
             .Returns(apiKey);
        return store;
    }

    private static MessageResponse BuildToolUseResponse(JsonObject toolInput, int inputTokens = 1200, int outputTokens = 240) => new()
    {
        Content = [ new ToolUseContent { Id = "tu_1", Name = "submit_job_match_score", Input = toolInput } ],
        Usage = new Usage { InputTokens = inputTokens, OutputTokens = outputTokens },
        StopReason = "tool_use",
        Model = "claude-haiku-4-5-20251001"
    };

    private static MessageResponse BuildTextOnlyResponse() => new()
    {
        Content = [ new TextContent { Text = "I cannot score this." } ],
        Usage = new Usage { InputTokens = 50, OutputTokens = 12 },
        StopReason = "end_turn"
    };

    [Fact]
    public async Task ScoreJobAsync_WithNoApiKey_ReturnsDefaultScore()
    {
        using var fixture = new SqliteFixture();
        await using var db = fixture.CreateContext();
        var factory = Substitute.For<IAnthropicClientFactory>();
        var notifications = Substitute.For<INotificationService>();
        var service = new ClaudeAiScoringService(db, BuildConfig(), BuildSecretStore(null), factory, notifications, NullLogger<ClaudeAiScoringService>.Instance);

        var job = new JobBuilder().Build();
        var profile = new ProfileBuilder().Build();

        var score = await service.ScoreJobAsync(job, profile);

        score.Score.Should().Be(5m);
        score.ModelVersion.Should().Be("default");
        factory.DidNotReceive().Create(Arg.Any<string>());
    }

    [Fact]
    public async Task ScoreJobAsync_HappyPath_PopulatesScoreSubScoresTokensAndKeywords()
    {
        using var fixture = new SqliteFixture();
        await using var db = fixture.CreateContext();
        var messenger = Substitute.For<IAnthropicMessenger>();
        var input = new JsonObject
        {
            ["score"] = 8.5m,
            ["skillsMatch"] = 9m,
            ["experienceFit"] = 8m,
            ["cultureFit"] = 7m,
            ["compensationFit"] = 6m,
            ["reasoning"] = "Strong skill overlap.",
            ["matchedKeywords"] = new JsonArray("C#", "AWS"),
            ["growthAreas"] = new JsonArray("Kafka"),
            ["redFlags"] = new JsonArray()
        };
        messenger.SendAsync(Arg.Any<MessageParameters>(), Arg.Any<CancellationToken>())
            .Returns(BuildToolUseResponse(input));

        var factory = Substitute.For<IAnthropicClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(messenger);

        var notifications = Substitute.For<INotificationService>();
        var service = new ClaudeAiScoringService(db, BuildConfig(), BuildSecretStore("test-key"), factory, notifications, NullLogger<ClaudeAiScoringService>.Instance);

        var score = await service.ScoreJobAsync(new JobBuilder().Build(), new ProfileBuilder().Build());

        score.Score.Should().Be(8.5m);
        score.SkillsMatchScore.Should().Be(9m);
        score.ExperienceFitScore.Should().Be(8m);
        score.CultureFitScore.Should().Be(7m);
        score.CompensationFitScore.Should().Be(6m);
        score.Reasoning.Should().Be("Strong skill overlap.");
        score.MatchedKeywords.Should().Contain("C#").And.Contain("AWS");
        score.GrowthAreas.Should().Contain("Kafka");
        score.InputTokens.Should().Be(1200);
        score.OutputTokens.Should().Be(240);
        score.ModelVersion.Should().Be("claude-haiku-4-5-20251001");
    }

    [Fact]
    public async Task ScoreJobAsync_WhenNoToolUseInResponse_FallsBackToDefault()
    {
        using var fixture = new SqliteFixture();
        await using var db = fixture.CreateContext();
        var messenger = Substitute.For<IAnthropicMessenger>();
        messenger.SendAsync(Arg.Any<MessageParameters>(), Arg.Any<CancellationToken>())
            .Returns(BuildTextOnlyResponse());
        var factory = Substitute.For<IAnthropicClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(messenger);

        var service = new ClaudeAiScoringService(db, BuildConfig(), BuildSecretStore("test-key"), factory, Substitute.For<INotificationService>(), NullLogger<ClaudeAiScoringService>.Instance);

        var score = await service.ScoreJobAsync(new JobBuilder().Build(), new ProfileBuilder().Build());

        score.Score.Should().Be(5m);
        score.ModelVersion.Should().Be("default");
    }

    [Fact]
    public async Task ScoreJobAsync_WhenInputMissingRequiredFields_ClampsToDefaults()
    {
        using var fixture = new SqliteFixture();
        await using var db = fixture.CreateContext();
        var input = new JsonObject(); // empty — all fields missing
        var messenger = Substitute.For<IAnthropicMessenger>();
        messenger.SendAsync(Arg.Any<MessageParameters>(), Arg.Any<CancellationToken>())
            .Returns(BuildToolUseResponse(input));
        var factory = Substitute.For<IAnthropicClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(messenger);

        var service = new ClaudeAiScoringService(db, BuildConfig(), BuildSecretStore("test-key"), factory, Substitute.For<INotificationService>(), NullLogger<ClaudeAiScoringService>.Instance);

        var score = await service.ScoreJobAsync(new JobBuilder().Build(), new ProfileBuilder().Build());

        score.Score.Should().Be(5m); // clamp([1, 10], 5 fallback) → 5
        score.Reasoning.Should().BeEmpty();
        score.MatchedKeywords.Should().Be("[]");
    }

    [Fact]
    public async Task ScoreJobAsync_WhenAnthropicCallThrows_ReturnsDefaultScore()
    {
        using var fixture = new SqliteFixture();
        await using var db = fixture.CreateContext();
        var messenger = Substitute.For<IAnthropicMessenger>();
        messenger.SendAsync(Arg.Any<MessageParameters>(), Arg.Any<CancellationToken>())
            .Returns<Task<MessageResponse>>(_ => throw new HttpRequestException("boom"));
        var factory = Substitute.For<IAnthropicClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(messenger);

        var service = new ClaudeAiScoringService(db, BuildConfig(), BuildSecretStore("test-key"), factory, Substitute.For<INotificationService>(), NullLogger<ClaudeAiScoringService>.Instance);

        var score = await service.ScoreJobAsync(new JobBuilder().Build(), new ProfileBuilder().Build());

        score.Score.Should().Be(5m);
        score.ModelVersion.Should().Be("default");
    }

    [Fact]
    public async Task BatchScoreAsync_SkipsJobsAlreadyScoredForProfile()
    {
        using var fixture = new SqliteFixture();
        await fixture.SeedUserAsync();
        await using var db = fixture.CreateContext();

        var profile = new ProfileBuilder().Build();
        var job = new JobBuilder().Build();
        db.SearchProfiles.Add(profile);
        db.Jobs.Add(job);
        db.AiScores.Add(new AiScoreBuilder().ForJob(job.Id).ForProfile(profile.Id).WithScore(6m).Build());
        await db.SaveChangesAsync();

        var factory = Substitute.For<IAnthropicClientFactory>();
        var service = new ClaudeAiScoringService(db, BuildConfig(), BuildSecretStore(null), factory, Substitute.For<INotificationService>(), NullLogger<ClaudeAiScoringService>.Instance);

        var result = await service.BatchScoreAsync([job], profile);

        result.Should().BeEmpty();
        factory.DidNotReceive().Create(Arg.Any<string>());
    }

    [Fact]
    public async Task BatchScoreAsync_EmitsNotification_WhenScoreCrossesEight()
    {
        using var fixture = new SqliteFixture();
        await fixture.SeedUserAsync();
        await using var db = fixture.CreateContext();

        var profile = new ProfileBuilder().Build();
        var job = new JobBuilder().Build();
        db.SearchProfiles.Add(profile);
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var messenger = Substitute.For<IAnthropicMessenger>();
        messenger.SendAsync(Arg.Any<MessageParameters>(), Arg.Any<CancellationToken>())
            .Returns(BuildToolUseResponse(new JsonObject
            {
                ["score"] = 9m, ["skillsMatch"] = 9m, ["experienceFit"] = 8m,
                ["cultureFit"] = 8m, ["compensationFit"] = 8m, ["reasoning"] = "Great fit."
            }));
        var factory = Substitute.For<IAnthropicClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(messenger);

        var notifications = Substitute.For<INotificationService>();
        var service = new ClaudeAiScoringService(db, BuildConfig(), BuildSecretStore("test-key"), factory, notifications, NullLogger<ClaudeAiScoringService>.Instance);

        await service.BatchScoreAsync([job], profile);

        await notifications.Received(1).OnHighScoreCreatedAsync(
            Arg.Is<AiScore>(s => s.Score >= 8m),
            Arg.Is<Job>(j => j.Id == job.Id),
            Arg.Is<SearchProfile>(p => p.Id == profile.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BatchScoreAsync_DoesNotEmitNotification_WhenScoreBelowEight()
    {
        using var fixture = new SqliteFixture();
        await fixture.SeedUserAsync();
        await using var db = fixture.CreateContext();

        var profile = new ProfileBuilder().Build();
        var job = new JobBuilder().Build();
        db.SearchProfiles.Add(profile);
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var messenger = Substitute.For<IAnthropicMessenger>();
        messenger.SendAsync(Arg.Any<MessageParameters>(), Arg.Any<CancellationToken>())
            .Returns(BuildToolUseResponse(new JsonObject
            {
                ["score"] = 6m, ["skillsMatch"] = 6m, ["experienceFit"] = 6m,
                ["cultureFit"] = 6m, ["compensationFit"] = 6m, ["reasoning"] = "Mediocre."
            }));
        var factory = Substitute.For<IAnthropicClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(messenger);

        var notifications = Substitute.For<INotificationService>();
        var service = new ClaudeAiScoringService(db, BuildConfig(), BuildSecretStore("test-key"), factory, notifications, NullLogger<ClaudeAiScoringService>.Instance);

        await service.BatchScoreAsync([job], profile);

        await notifications.DidNotReceive().OnHighScoreCreatedAsync(
            Arg.Any<AiScore>(), Arg.Any<Job>(), Arg.Any<SearchProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScoreJobAsync_UsesProfilePreferredModel_WhenSet()
    {
        using var fixture = new SqliteFixture();
        await using var db = fixture.CreateContext();

        var profile = new ProfileBuilder().WithPreferredModel("claude-sonnet-4-6").Build();
        MessageParameters? captured = null;
        var messenger = Substitute.For<IAnthropicMessenger>();
        messenger.SendAsync(Arg.Do<MessageParameters>(p => captured = p), Arg.Any<CancellationToken>())
            .Returns(BuildToolUseResponse(new JsonObject
            {
                ["score"] = 7m, ["skillsMatch"] = 7m, ["experienceFit"] = 7m,
                ["cultureFit"] = 7m, ["compensationFit"] = 7m, ["reasoning"] = "OK."
            }));
        var factory = Substitute.For<IAnthropicClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(messenger);

        var service = new ClaudeAiScoringService(db, BuildConfig(), BuildSecretStore("test-key"), factory, Substitute.For<INotificationService>(), NullLogger<ClaudeAiScoringService>.Instance);

        var score = await service.ScoreJobAsync(new JobBuilder().Build(), profile);

        captured.Should().NotBeNull();
        captured!.Model.Should().Be("claude-sonnet-4-6");
        score.ModelVersion.Should().Be("claude-sonnet-4-6");
    }

    [Fact]
    public async Task ScoreJobAsync_EmbedsMostRecentTenRatings_AsFewShotExamples()
    {
        using var fixture = new SqliteFixture();
        await fixture.SeedUserAsync();
        await using var db = fixture.CreateContext();

        var profile = new ProfileBuilder().Build();
        var jobs = Enumerable.Range(0, 12).Select(i => new JobBuilder().WithTitle($"Job {i}").Build()).ToList();
        db.SearchProfiles.Add(profile);
        db.Jobs.AddRange(jobs);
        for (int i = 0; i < 12; i++)
        {
            db.UserRatings.Add(new UserRatingBuilder()
                .ForJob(jobs[i].Id)
                .ForProfile(profile.Id)
                .WithStars((i % 5) + 1)
                .RatedAt(DateTime.UtcNow.AddDays(-i))
                .Build());
        }
        await db.SaveChangesAsync();

        MessageParameters? captured = null;
        var messenger = Substitute.For<IAnthropicMessenger>();
        messenger.SendAsync(Arg.Do<MessageParameters>(p => captured = p), Arg.Any<CancellationToken>())
            .Returns(BuildToolUseResponse(new JsonObject
            {
                ["score"] = 7m, ["skillsMatch"] = 7m, ["experienceFit"] = 7m,
                ["cultureFit"] = 7m, ["compensationFit"] = 7m, ["reasoning"] = "OK."
            }));
        var factory = Substitute.For<IAnthropicClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(messenger);

        var service = new ClaudeAiScoringService(db, BuildConfig(), BuildSecretStore("test-key"), factory, Substitute.For<INotificationService>(), NullLogger<ClaudeAiScoringService>.Instance);

        await service.ScoreJobAsync(new JobBuilder().Build(), profile);

        captured.Should().NotBeNull();
        var systemText = string.Join("\n", captured!.System.Select(s => s.Text));
        // Most recent 10 (Job 0..Job 9) included
        for (int i = 0; i < 10; i++)
            systemText.Should().Contain($"Job {i}");
        // 11th and 12th oldest NOT included
        systemText.Should().NotContain("Job 10");
        systemText.Should().NotContain("Job 11");
    }
}
