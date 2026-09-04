using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI_powerd_job_search_management_system.Migrations
{
    /// <inheritdoc />
    public partial class AddJobIdToNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "JobId",
                table: "Notifications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_JobId",
                table: "Notifications",
                column: "JobId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Jobs_JobId",
                table: "Notifications",
                column: "JobId",
                principalTable: "Jobs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Jobs_JobId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_JobId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "JobId",
                table: "Notifications");
        }
    }
}
