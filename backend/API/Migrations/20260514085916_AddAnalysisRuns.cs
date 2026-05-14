using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AICourseTester.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnalysisRunId",
                table: "ErrorRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnalysisRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaskType = table.Column<string>(type: "text", nullable: false),
                    AlphaBetaId = table.Column<int>(type: "integer", nullable: true),
                    FifteenPuzzleId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AnalyzerVersion = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorRecords_AnalysisRunId",
                table: "ErrorRecords",
                column: "AnalysisRunId");

            migrationBuilder.AddForeignKey(
                name: "FK_ErrorRecords_AnalysisRuns_AnalysisRunId",
                table: "ErrorRecords",
                column: "AnalysisRunId",
                principalTable: "AnalysisRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ErrorRecords_AnalysisRuns_AnalysisRunId",
                table: "ErrorRecords");

            migrationBuilder.DropTable(
                name: "AnalysisRuns");

            migrationBuilder.DropIndex(
                name: "IX_ErrorRecords_AnalysisRunId",
                table: "ErrorRecords");

            migrationBuilder.DropColumn(
                name: "AnalysisRunId",
                table: "ErrorRecords");
        }
    }
}
