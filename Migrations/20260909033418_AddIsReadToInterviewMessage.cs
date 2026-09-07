using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI_powerd_job_search_management_system.Migrations
{
    /// <inheritdoc />
    public partial class AddIsReadToInterviewMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "InterviewMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "InterviewMessages");
        }
    }
}
