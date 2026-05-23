using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobScout.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StatusHistory",
                table: "JobApplications",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_JobId_ProfileId",
                table: "JobApplications",
                columns: new[] { "JobId", "ProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_ProfileId",
                table: "JobApplications",
                column: "ProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_JobApplications_Jobs_JobId",
                table: "JobApplications",
                column: "JobId",
                principalTable: "Jobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_JobApplications_SearchProfiles_ProfileId",
                table: "JobApplications",
                column: "ProfileId",
                principalTable: "SearchProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobApplications_Jobs_JobId",
                table: "JobApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_JobApplications_SearchProfiles_ProfileId",
                table: "JobApplications");

            migrationBuilder.DropIndex(
                name: "IX_JobApplications_JobId_ProfileId",
                table: "JobApplications");

            migrationBuilder.DropIndex(
                name: "IX_JobApplications_ProfileId",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "StatusHistory",
                table: "JobApplications");
        }
    }
}
