using JobScout.Core.Enums;
using JobScout.Core.Models;
using JobScout.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace JobScout.Infrastructure.Data;

public class JobScoutDbContext : IdentityDbContext<ApplicationUser>
{
    public JobScoutDbContext(DbContextOptions<JobScoutDbContext> options) : base(options) { }

    public DbSet<SearchProfile> SearchProfiles => Set<SearchProfile>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<AiScore> AiScores => Set<AiScore>();
    public DbSet<UserRating> UserRatings => Set<UserRating>();
    public DbSet<DailyMetric> DailyMetrics => Set<DailyMetric>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasKey(j => j.Id);
            entity.Property(j => j.Tags).HasColumnType("TEXT");
            entity.Property(j => j.Source).HasConversion<string>();
            entity.Property(j => j.LocationType).HasConversion<string>();
            entity.Property(j => j.JobType).HasConversion<string>();
            entity.HasIndex(j => new { j.ExternalId, j.Source }).IsUnique();
        });

        modelBuilder.Entity<AiScore>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.MatchedKeywords).HasColumnType("TEXT");
            entity.Property(a => a.Score).HasPrecision(4, 2);
            entity.HasOne(a => a.Job).WithMany(j => j.AiScores).HasForeignKey(a => a.JobId);
            entity.HasOne(a => a.Profile).WithMany(p => p.AiScores).HasForeignKey(a => a.ProfileId);
        });

        modelBuilder.Entity<UserRating>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => new { r.JobId, r.ProfileId }).IsUnique();
            entity.HasOne(r => r.Job).WithMany(j => j.UserRatings).HasForeignKey(r => r.JobId);
            entity.HasOne(r => r.Profile).WithMany(p => p.UserRatings).HasForeignKey(r => r.ProfileId);
        });

        modelBuilder.Entity<DailyMetric>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Source).HasConversion<string>();
            entity.HasIndex(d => new { d.ProfileId, d.Date, d.Source }).IsUnique();
            entity.HasOne(d => d.Profile).WithMany(p => p.DailyMetrics).HasForeignKey(d => d.ProfileId);
        });

        modelBuilder.Entity<SearchProfile>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.UserId);
            entity.HasOne<ApplicationUser>()
                  .WithMany(u => u.Profiles)
                  .HasForeignKey(p => p.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Phase 2: JSON-serialized list columns with value comparers
            var stringListComparer = new ValueComparer<List<string>>(
                (a, b) => a != null && b != null && a.SequenceEqual(b),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList());

            var sourceListComparer = new ValueComparer<List<JobSource>>(
                (a, b) => a != null && b != null && a.SequenceEqual(b),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList());

            entity.Property(p => p.SearchKeywords).HasColumnType("TEXT")
                  .HasConversion(
                      v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                      v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new())
                  .Metadata.SetValueComparer(stringListComparer);

            entity.Property(p => p.PreferredSources).HasColumnType("TEXT")
                  .HasConversion(
                      v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                      v => System.Text.Json.JsonSerializer.Deserialize<List<JobSource>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new())
                  .Metadata.SetValueComparer(sourceListComparer);

            entity.Property(p => p.PreferredJobTypes).HasColumnType("TEXT")
                  .HasConversion(
                      v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                      v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new())
                  .Metadata.SetValueComparer(stringListComparer);

            entity.Property(p => p.PreferredLocationTypes).HasColumnType("TEXT")
                  .HasConversion(
                      v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                      v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new())
                  .Metadata.SetValueComparer(stringListComparer);

            entity.Property(p => p.DetectedSkills).HasColumnType("TEXT")
                  .HasConversion(
                      v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                      v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new())
                  .Metadata.SetValueComparer(stringListComparer);
        });

        modelBuilder.Entity<JobApplication>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Status).HasConversion<string>();
        });
    }
}
