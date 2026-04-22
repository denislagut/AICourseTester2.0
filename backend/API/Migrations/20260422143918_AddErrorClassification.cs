using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AICourseTester.Migrations
{
    /// <inheritdoc />
    public partial class AddErrorClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ErrorTypeId",
                table: "ErrorRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ErrorTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DefaultSeverity = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeAspects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    TopicName = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeAspects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ErrorTypeAspects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ErrorTypeId = table.Column<int>(type: "integer", nullable: false),
                    KnowledgeAspectId = table.Column<int>(type: "integer", nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorTypeAspects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErrorTypeAspects_ErrorTypes_ErrorTypeId",
                        column: x => x.ErrorTypeId,
                        principalTable: "ErrorTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ErrorTypeAspects_KnowledgeAspects_KnowledgeAspectId",
                        column: x => x.KnowledgeAspectId,
                        principalTable: "KnowledgeAspects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorRecords_ErrorTypeId",
                table: "ErrorRecords",
                column: "ErrorTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorTypeAspects_ErrorTypeId",
                table: "ErrorTypeAspects",
                column: "ErrorTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorTypeAspects_KnowledgeAspectId",
                table: "ErrorTypeAspects",
                column: "KnowledgeAspectId");

            migrationBuilder.AddForeignKey(
                name: "FK_ErrorRecords_ErrorTypes_ErrorTypeId",
                table: "ErrorRecords",
                column: "ErrorTypeId",
                principalTable: "ErrorTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ErrorRecords_ErrorTypes_ErrorTypeId",
                table: "ErrorRecords");

            migrationBuilder.DropTable(
                name: "ErrorTypeAspects");

            migrationBuilder.DropTable(
                name: "ErrorTypes");

            migrationBuilder.DropTable(
                name: "KnowledgeAspects");

            migrationBuilder.DropIndex(
                name: "IX_ErrorRecords_ErrorTypeId",
                table: "ErrorRecords");

            migrationBuilder.DropColumn(
                name: "ErrorTypeId",
                table: "ErrorRecords");
        }
    }
}
