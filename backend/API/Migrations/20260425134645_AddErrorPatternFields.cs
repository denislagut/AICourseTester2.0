using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AICourseTester.Migrations
{
    /// <inheritdoc />
    public partial class AddErrorPatternFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PatternType",
                table: "ErrorRecords",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SimilarErrorCount",
                table: "ErrorRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "SimilarErrorRatio",
                table: "ErrorRecords",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "SimilarOpportunityCount",
                table: "ErrorRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PatternType",
                table: "ErrorRecords");

            migrationBuilder.DropColumn(
                name: "SimilarErrorCount",
                table: "ErrorRecords");

            migrationBuilder.DropColumn(
                name: "SimilarErrorRatio",
                table: "ErrorRecords");

            migrationBuilder.DropColumn(
                name: "SimilarOpportunityCount",
                table: "ErrorRecords");
        }
    }
}
