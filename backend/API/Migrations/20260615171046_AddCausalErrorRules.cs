using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace API.Migrations
{
    /// <inheritdoc />
    public partial class AddCausalErrorRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CausalErrorRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaskType = table.Column<string>(type: "text", nullable: false),
                    SourceErrorCode = table.Column<string>(type: "text", nullable: false),
                    TargetErrorCode = table.Column<string>(type: "text", nullable: false),
                    RelationType = table.Column<string>(type: "text", nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    SameNodeRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SameRootBranchRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CausalErrorRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CausalErrorRules_TaskType_SourceErrorCode_TargetErrorCode_R~",
                table: "CausalErrorRules",
                columns: new[] { "TaskType", "SourceErrorCode", "TargetErrorCode", "RelationType", "SameNodeRequired", "SameRootBranchRequired" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CausalErrorRules");
        }
    }
}
