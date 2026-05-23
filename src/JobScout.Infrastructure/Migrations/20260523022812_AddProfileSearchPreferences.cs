using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobScout.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileSearchPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DetectedSkills",
                table: "SearchProfiles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LocationPreference",
                table: "SearchProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredJobTypes",
                table: "SearchProfiles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PreferredLocationTypes",
                table: "SearchProfiles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PreferredSources",
                table: "SearchProfiles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProfileColor",
                table: "SearchProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SearchKeywords",
                table: "SearchProfiles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DetectedSkills",
                table: "SearchProfiles");

            migrationBuilder.DropColumn(
                name: "LocationPreference",
                table: "SearchProfiles");

            migrationBuilder.DropColumn(
                name: "PreferredJobTypes",
                table: "SearchProfiles");

            migrationBuilder.DropColumn(
                name: "PreferredLocationTypes",
                table: "SearchProfiles");

            migrationBuilder.DropColumn(
                name: "PreferredSources",
                table: "SearchProfiles");

            migrationBuilder.DropColumn(
                name: "ProfileColor",
                table: "SearchProfiles");

            migrationBuilder.DropColumn(
                name: "SearchKeywords",
                table: "SearchProfiles");
        }
    }
}
