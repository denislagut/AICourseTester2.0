using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AICourseTester.Migrations
{
    /// <inheritdoc />
    public partial class AddCausalErrorLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CausalErrorLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceErrorId = table.Column<int>(type: "integer", nullable: false),
                    TargetErrorId = table.Column<int>(type: "integer", nullable: false),
                    RelationType = table.Column<string>(type: "text", nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CausalErrorLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CausalErrorLinks_ErrorRecords_SourceErrorId",
                        column: x => x.SourceErrorId,
                        principalTable: "ErrorRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CausalErrorLinks_ErrorRecords_TargetErrorId",
                        column: x => x.TargetErrorId,
                        principalTable: "ErrorRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CausalErrorLinks_SourceErrorId",
                table: "CausalErrorLinks",
                column: "SourceErrorId");

            migrationBuilder.CreateIndex(
                name: "IX_CausalErrorLinks_TargetErrorId",
                table: "CausalErrorLinks",
                column: "TargetErrorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CausalErrorLinks");
        }
    }
}
