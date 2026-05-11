using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AICourseTester.Migrations
{
    /// <inheritdoc />
    public partial class AddFifteenPuzzleAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FifteenPuzzleId",
                table: "KnowledgeGaps",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaskType",
                table: "KnowledgeGaps",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "AlphaBetaId",
                table: "ErrorRecords",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "FifteenPuzzleId",
                table: "ErrorRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaskType",
                table: "ErrorRecords",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeGaps_FifteenPuzzleId",
                table: "KnowledgeGaps",
                column: "FifteenPuzzleId");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorRecords_FifteenPuzzleId",
                table: "ErrorRecords",
                column: "FifteenPuzzleId");

            migrationBuilder.AddForeignKey(
                name: "FK_ErrorRecords_Fifteens_FifteenPuzzleId",
                table: "ErrorRecords",
                column: "FifteenPuzzleId",
                principalTable: "Fifteens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeGaps_Fifteens_FifteenPuzzleId",
                table: "KnowledgeGaps",
                column: "FifteenPuzzleId",
                principalTable: "Fifteens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ErrorRecords_Fifteens_FifteenPuzzleId",
                table: "ErrorRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeGaps_Fifteens_FifteenPuzzleId",
                table: "KnowledgeGaps");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeGaps_FifteenPuzzleId",
                table: "KnowledgeGaps");

            migrationBuilder.DropIndex(
                name: "IX_ErrorRecords_FifteenPuzzleId",
                table: "ErrorRecords");

            migrationBuilder.DropColumn(
                name: "FifteenPuzzleId",
                table: "KnowledgeGaps");

            migrationBuilder.DropColumn(
                name: "TaskType",
                table: "KnowledgeGaps");

            migrationBuilder.DropColumn(
                name: "FifteenPuzzleId",
                table: "ErrorRecords");

            migrationBuilder.DropColumn(
                name: "TaskType",
                table: "ErrorRecords");

            migrationBuilder.AlterColumn<int>(
                name: "AlphaBetaId",
                table: "ErrorRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
