using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobScout.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiScoringEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DesiredSalaryMax",
                table: "SearchProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DesiredSalaryMin",
                table: "SearchProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredModel",
                table: "SearchProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CompensationFitScore",
                table: "AiScores",
                type: "TEXT",
                precision: 4,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CultureFitScore",
                table: "AiScores",
                type: "TEXT",
                precision: 4,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedCostUsd",
                table: "AiScores",
                type: "TEXT",
                precision: 12,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExperienceFitScore",
                table: "AiScores",
                type: "TEXT",
                precision: 4,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GrowthAreas",
                table: "AiScores",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "InputTokens",
                table: "AiScores",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OutputTokens",
                table: "AiScores",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RedFlags",
                table: "AiScores",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "SkillsMatchScore",
                table: "AiScores",
                type: "TEXT",
                precision: 4,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DesiredSalaryMax",
                table: "SearchProfiles");

            migrationBuilder.DropColumn(
                name: "DesiredSalaryMin",
                table: "SearchProfiles");

            migrationBuilder.DropColumn(
                name: "PreferredModel",
                table: "SearchProfiles");

            migrationBuilder.DropColumn(
                name: "CompensationFitScore",
                table: "AiScores");

            migrationBuilder.DropColumn(
                name: "CultureFitScore",
                table: "AiScores");

            migrationBuilder.DropColumn(
                name: "EstimatedCostUsd",
                table: "AiScores");

            migrationBuilder.DropColumn(
                name: "ExperienceFitScore",
                table: "AiScores");

            migrationBuilder.DropColumn(
                name: "GrowthAreas",
                table: "AiScores");

            migrationBuilder.DropColumn(
                name: "InputTokens",
                table: "AiScores");

            migrationBuilder.DropColumn(
                name: "OutputTokens",
                table: "AiScores");

            migrationBuilder.DropColumn(
                name: "RedFlags",
                table: "AiScores");

            migrationBuilder.DropColumn(
                name: "SkillsMatchScore",
                table: "AiScores");
        }
    }
}
