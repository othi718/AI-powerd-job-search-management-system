using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI_powerd_job_search_management_system.Migrations
{
    /// <inheritdoc />
    public partial class AddAIInsightToAIAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AIInsight",
                table: "AIAnalyses",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AIInsight",
                table: "AIAnalyses");
        }
    }
}
