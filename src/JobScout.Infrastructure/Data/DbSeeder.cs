using JobScout.Core.Enums;
using JobScout.Core.Models;
using JobScout.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JobScout.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobScoutDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<JobScoutDbContext>>();

        await db.Database.MigrateAsync();

        if (await db.SearchProfiles.AnyAsync())
        {
            logger.LogInformation("Database already seeded — skipping.");
            return;
        }

        logger.LogInformation("Seeding development data...");

        // Create dev user (dev@jobscout.local / DevPass123!)
        var devUser = new ApplicationUser
        {
            UserName = "dev@jobscout.local",
            Email = "dev@jobscout.local",
            DisplayName = "Matthew Wilson",
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };

        var createResult = await userManager.CreateAsync(devUser, "DevPass123!");
        if (!createResult.Succeeded)
        {
            logger.LogError("Failed to create dev user: {Errors}",
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        var profileId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var profile = new SearchProfile
        {
            Id = profileId,
            Name = "Software Engineering",
            Description = "Senior .NET / Azure roles, open to remote or hybrid",
            UserId = devUser.Id,
            ResumeText = """
                Matthew Wilson — Senior Software Engineer
                10 years of experience building cloud-native applications on Azure using C#, .NET, ASP.NET Core,
                Entity Framework Core, SQL Server, and Blazor. Led teams of 4–8 engineers. Strong background
                in microservices, REST API design, CI/CD pipelines (GitHub Actions, Azure DevOps), and
                containerisation with Docker and Kubernetes. Also experienced with Vue.js and TypeScript
                on the frontend. Open to full-time or contract engagements.
                Skills: C#, .NET 8, ASP.NET Core, Blazor, Azure, EF Core, SQL Server, Docker, Kubernetes,
                REST APIs, GraphQL, Vue.js, TypeScript, GitHub Actions, Azure DevOps, Terraform.
                """,
            ResumeFileName = "matthew-wilson-resume.txt",
            CreatedAt = now.AddDays(-30),
            UpdatedAt = now.AddDays(-5),
            IsActive = true
        };

        await db.SearchProfiles.AddAsync(profile);

        var jobs = new List<Job>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ExternalId = "rjk-001",
                Title = "Senior .NET Engineer",
                Company = "Acme Corp",
                Location = "Remote, US",
                LocationType = LocationType.Remote,
                JobType = JobType.FullTime,
                Description = "Join our platform team to build high-throughput C# microservices on Azure Service Bus. We use .NET 8, EF Core, and Docker. You will own the entire service lifecycle from design to production.",
                Tags = """["C#",".NET","Azure","Docker","Microservices","EF Core"]""",
                Salary = "$140,000–$175,000",
                PostedAt = now.AddDays(-3),
                DiscoveredAt = now.AddDays(-2),
                Source = JobSource.RemoteOK,
                SourceUrl = "https://remoteok.com/jobs/rjk-001",
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                ExternalId = "adzuna-2847301",
                Title = "Lead Backend Engineer — Blazor + ASP.NET Core",
                Company = "FinTech Innovations",
                Location = "New York, NY",
                LocationType = LocationType.Hybrid,
                JobType = JobType.FullTime,
                Description = "We are rebuilding our client portal in Blazor WebAssembly backed by an ASP.NET Core 8 API. Looking for a senior engineer comfortable with EF Core, SignalR, and Azure SQL. 3 days on-site, 2 remote.",
                Tags = """["C#","Blazor","ASP.NET Core","SignalR","Azure SQL","EF Core"]""",
                Salary = "$155,000–$185,000",
                PostedAt = now.AddDays(-5),
                DiscoveredAt = now.AddDays(-4),
                Source = JobSource.Adzuna,
                SourceUrl = "https://adzuna.com/jobs/2847301",
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                ExternalId = "muse-4492",
                Title = "Cloud Platform Engineer",
                Company = "Stratos Labs",
                Location = "Remote — Anywhere",
                LocationType = LocationType.Remote,
                JobType = JobType.FullTime,
                Description = "Build and maintain our Azure-based infrastructure. Terraform, AKS, Azure DevOps pipelines, .NET 8 services. You will work closely with 4 product squads as the platform SME.",
                Tags = """["Azure","Terraform","AKS","Kubernetes","Docker",".NET","Azure DevOps"]""",
                Salary = "$130,000–$160,000",
                PostedAt = now.AddDays(-7),
                DiscoveredAt = now.AddDays(-6),
                Source = JobSource.TheMuse,
                SourceUrl = "https://themuse.com/jobs/4492",
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                ExternalId = "rjk-042",
                Title = "Full-Stack .NET + Vue.js Developer",
                Company = "Bright Digital",
                Location = "Remote, EU/US",
                LocationType = LocationType.Remote,
                JobType = JobType.Contract,
                Description = "6-month contract. Building a SaaS product with Vue 3 on the front end and .NET 8 Web API on the back end. CI/CD via GitHub Actions. Daily standups, async-first culture.",
                Tags = """["C#",".NET","Vue.js","TypeScript","GitHub Actions","REST API"]""",
                Salary = "$80–$100/hr",
                PostedAt = now.AddDays(-1),
                DiscoveredAt = now,
                Source = JobSource.RemoteOK,
                SourceUrl = "https://remoteok.com/jobs/rjk-042",
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                ExternalId = "adzuna-2851900",
                Title = "Software Engineer — Python / ML Pipelines",
                Company = "DataStream AI",
                Location = "San Francisco, CA",
                LocationType = LocationType.OnSite,
                JobType = JobType.FullTime,
                Description = "Building ML data pipelines in Python using Airflow and Spark. No .NET, no Azure — pure Python + AWS stack. On-site in SF required.",
                Tags = """["Python","Airflow","Spark","AWS","Machine Learning"]""",
                Salary = "$145,000–$170,000",
                PostedAt = now.AddDays(-10),
                DiscoveredAt = now.AddDays(-9),
                Source = JobSource.Adzuna,
                SourceUrl = "https://adzuna.com/jobs/2851900",
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                ExternalId = "muse-5018",
                Title = "Senior Software Engineer — GraphQL / .NET",
                Company = "Vantage Systems",
                Location = "Austin, TX (remote-friendly)",
                LocationType = LocationType.Hybrid,
                JobType = JobType.FullTime,
                Description = "Design and build our GraphQL API layer in .NET 8. Experience with Hot Chocolate or Strawberry Shake a plus. Entity Framework Core, PostgreSQL, Azure.",
                Tags = """["C#",".NET","GraphQL","Hot Chocolate","EF Core","PostgreSQL","Azure"]""",
                Salary = "$135,000–$165,000",
                PostedAt = now.AddDays(-4),
                DiscoveredAt = now.AddDays(-3),
                Source = JobSource.TheMuse,
                SourceUrl = "https://themuse.com/jobs/5018",
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                ExternalId = "rjk-089",
                Title = "Principal Engineer — Distributed Systems",
                Company = "Meridian Cloud",
                Location = "Remote",
                LocationType = LocationType.Remote,
                JobType = JobType.FullTime,
                Description = "Join us as a Principal Engineer leading the architecture of our distributed event-driven platform built on .NET 8, Azure Service Bus, and Cosmos DB. You will define standards, review designs, and mentor 3 teams.",
                Tags = """["C#",".NET","Azure Service Bus","Cosmos DB","Distributed Systems","Event-Driven"]""",
                Salary = "$190,000–$230,000",
                PostedAt = now.AddDays(-2),
                DiscoveredAt = now.AddDays(-1),
                Source = JobSource.RemoteOK,
                SourceUrl = "https://remoteok.com/jobs/rjk-089",
                IsActive = true
            }
        };

        await db.Jobs.AddRangeAsync(jobs);

        var aiScores = new List<AiScore>
        {
            new() { Id = Guid.NewGuid(), JobId = jobs[0].Id, ProfileId = profileId, Score = 9.2m, Reasoning = "Strong match — C#, .NET 8, Azure, EF Core, Docker are all core skills. Remote role fits preferences. Microservices architecture matches your background.", MatchedKeywords = """["C#",".NET 8","Azure","Docker","EF Core","Microservices"]""", ScoredAt = now.AddDays(-2), ModelVersion = "claude-sonnet-4-20250514" },
            new() { Id = Guid.NewGuid(), JobId = jobs[1].Id, ProfileId = profileId, Score = 8.7m, Reasoning = "Excellent fit — Blazor WebAssembly and ASP.NET Core 8 are a direct match to your recent project work. Hybrid schedule and strong comp range are positives. SignalR is a nice bonus.", MatchedKeywords = """["Blazor","ASP.NET Core","EF Core","Azure SQL","SignalR"]""", ScoredAt = now.AddDays(-4), ModelVersion = "claude-sonnet-4-20250514" },
            new() { Id = Guid.NewGuid(), JobId = jobs[2].Id, ProfileId = profileId, Score = 7.8m, Reasoning = "Good match on Azure and DevOps skills. Terraform and AKS are in your wheelhouse, though the role skews more infra than pure development. Solid if you want to move platform-side.", MatchedKeywords = """["Azure","Terraform","AKS","Kubernetes",".NET","Azure DevOps"]""", ScoredAt = now.AddDays(-6), ModelVersion = "claude-sonnet-4-20250514" },
            new() { Id = Guid.NewGuid(), JobId = jobs[3].Id, ProfileId = profileId, Score = 8.1m, Reasoning = "Good contract fit — Vue.js and .NET 8 match well. GitHub Actions is familiar. Contract rate is fair for remote EU/US. Duration is short but solid for the portfolio.", MatchedKeywords = """["C#",".NET","Vue.js","TypeScript","GitHub Actions"]""", ScoredAt = now.AddDays(0), ModelVersion = "claude-sonnet-4-20250514" },
            new() { Id = Guid.NewGuid(), JobId = jobs[4].Id, ProfileId = profileId, Score = 2.4m, Reasoning = "Poor fit — Python/ML/AWS stack has no overlap with your C#/.NET/Azure background. On-site in SF adds friction. Skip unless you're actively pivoting.", MatchedKeywords = """[]""", ScoredAt = now.AddDays(-9), ModelVersion = "claude-sonnet-4-20250514" },
            new() { Id = Guid.NewGuid(), JobId = jobs[5].Id, ProfileId = profileId, Score = 8.4m, Reasoning = "Solid match — GraphQL in .NET is a gap you've mentioned wanting to close. EF Core and Azure are familiar. Strawberry Shake is niche but learnable. Remote-friendly Texas role.", MatchedKeywords = """["C#",".NET","GraphQL","EF Core","Azure"]""", ScoredAt = now.AddDays(-3), ModelVersion = "claude-sonnet-4-20250514" },
            new() { Id = Guid.NewGuid(), JobId = jobs[6].Id, ProfileId = profileId, Score = 9.5m, Reasoning = "Near-perfect match — Principal Engineer level, distributed .NET systems on Azure, remote. Cosmos DB and Service Bus are exactly what you have been working with. Comp range is exceptional.", MatchedKeywords = """["C#",".NET","Azure Service Bus","Cosmos DB","Distributed Systems"]""", ScoredAt = now.AddDays(-1), ModelVersion = "claude-sonnet-4-20250514" }
        };

        await db.AiScores.AddRangeAsync(aiScores);

        var userRatings = new List<UserRating>
        {
            new() { Id = Guid.NewGuid(), JobId = jobs[0].Id, ProfileId = profileId, Stars = 5, Notes = "Perfect stack, going to apply today.", RatedAt = now.AddDays(-1) },
            new() { Id = Guid.NewGuid(), JobId = jobs[1].Id, ProfileId = profileId, Stars = 4, Notes = "Love the Blazor focus, slightly concerned about on-site days.", RatedAt = now.AddDays(-3) },
            new() { Id = Guid.NewGuid(), JobId = jobs[4].Id, ProfileId = profileId, Stars = 1, Notes = "Not relevant — Python/ML is not my direction.", RatedAt = now.AddDays(-8) }
        };

        await db.UserRatings.AddRangeAsync(userRatings);

        var sources = Enum.GetValues<JobSource>();
        var metrics = new List<DailyMetric>();
        var random = new Random(42);

        for (var day = 13; day >= 0; day--)
        {
            var date = DateOnly.FromDateTime(now.AddDays(-day));
            foreach (var source in sources)
            {
                var jobsFound = random.Next(2, 18);
                var strongFits = random.Next(0, Math.Min(jobsFound, 6));
                metrics.Add(new DailyMetric
                {
                    Id = Guid.NewGuid(),
                    ProfileId = profileId,
                    Date = date,
                    Source = source,
                    JobsFound = jobsFound,
                    StrongFits = strongFits,
                    UserLiked = random.Next(0, Math.Min(strongFits + 1, 4)),
                    Applied = random.Next(0, 2)
                });
            }
        }

        await db.DailyMetrics.AddRangeAsync(metrics);
        await db.SaveChangesAsync();

        logger.LogInformation("Seed complete: 1 profile, {Jobs} jobs, {Scores} AI scores, {Ratings} ratings, {Metrics} daily metric records.",
            jobs.Count, aiScores.Count, userRatings.Count, metrics.Count);
    }
}
