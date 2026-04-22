using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AICourseTester.Migrations
{
    /// <inheritdoc />
    public partial class AddErrorRecordContextFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsExpectedPruned",
                table: "ErrorRecords",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnCorrectPath",
                table: "ErrorRecords",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsUserPruned",
                table: "ErrorRecords",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RootBranchId",
                table: "ErrorRecords",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsExpectedPruned",
                table: "ErrorRecords");

            migrationBuilder.DropColumn(
                name: "IsOnCorrectPath",
                table: "ErrorRecords");

            migrationBuilder.DropColumn(
                name: "IsUserPruned",
                table: "ErrorRecords");

            migrationBuilder.DropColumn(
                name: "RootBranchId",
                table: "ErrorRecords");
        }
    }
}
