using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeGapDynamics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnalysisRunId",
                table: "KnowledgeGaps",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GapScoreDelta",
                table: "KnowledgeGaps",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PreviousGapScore",
                table: "KnowledgeGaps",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Trend",
                table: "KnowledgeGaps",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeGaps_AnalysisRunId",
                table: "KnowledgeGaps",
                column: "AnalysisRunId");

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeGaps_AnalysisRuns_AnalysisRunId",
                table: "KnowledgeGaps",
                column: "AnalysisRunId",
                principalTable: "AnalysisRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeGaps_AnalysisRuns_AnalysisRunId",
                table: "KnowledgeGaps");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeGaps_AnalysisRunId",
                table: "KnowledgeGaps");

            migrationBuilder.DropColumn(
                name: "AnalysisRunId",
                table: "KnowledgeGaps");

            migrationBuilder.DropColumn(
                name: "GapScoreDelta",
                table: "KnowledgeGaps");

            migrationBuilder.DropColumn(
                name: "PreviousGapScore",
                table: "KnowledgeGaps");

            migrationBuilder.DropColumn(
                name: "Trend",
                table: "KnowledgeGaps");
        }
    }
}
