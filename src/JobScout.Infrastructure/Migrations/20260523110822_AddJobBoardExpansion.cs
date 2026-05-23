using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobScout.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJobBoardExpansion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AlternateSourceUrls",
                table: "Jobs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "DuplicateOfJobId",
                table: "Jobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPotentialDuplicate",
                table: "Jobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CustomJobSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    FeedUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Format = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    JsonJobsPath = table.Column<string>(type: "TEXT", nullable: true),
                    JsonTitleField = table.Column<string>(type: "TEXT", nullable: true),
                    JsonCompanyField = table.Column<string>(type: "TEXT", nullable: true),
                    JsonLocationField = table.Column<string>(type: "TEXT", nullable: true),
                    JsonDescriptionField = table.Column<string>(type: "TEXT", nullable: true),
                    JsonUrlField = table.Column<string>(type: "TEXT", nullable: true),
                    JsonPostedAtField = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomJobSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomJobSources_SearchProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "SearchProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomJobSources_ProfileId",
                table: "CustomJobSources",
                column: "ProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomJobSources");

            migrationBuilder.DropColumn(
                name: "AlternateSourceUrls",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "DuplicateOfJobId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "IsPotentialDuplicate",
                table: "Jobs");
        }
    }
}
