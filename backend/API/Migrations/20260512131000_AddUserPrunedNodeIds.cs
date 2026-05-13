using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AICourseTester.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPrunedNodeIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserPrunedNodeIds",
                table: "AlphaBeta",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserPrunedNodeIds",
                table: "AlphaBeta");
        }
    }
}
