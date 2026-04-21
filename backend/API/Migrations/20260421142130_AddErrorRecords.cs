using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AICourseTester.Migrations
{
    /// <inheritdoc />
    public partial class AddErrorRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ErrorRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AlphaBetaId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    NodeId = table.Column<int>(type: "integer", nullable: true),
                    TreeLevel = table.Column<int>(type: "integer", nullable: true),
                    ElementType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpectedA = table.Column<int>(type: "integer", nullable: true),
                    ActualA = table.Column<int>(type: "integer", nullable: true),
                    ExpectedB = table.Column<int>(type: "integer", nullable: true),
                    ActualB = table.Column<int>(type: "integer", nullable: true),
                    PathStepIndex = table.Column<int>(type: "integer", nullable: true),
                    ExpectedPathNodeId = table.Column<int>(type: "integer", nullable: true),
                    ActualPathNodeId = table.Column<int>(type: "integer", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    SeverityScore = table.Column<double>(type: "double precision", nullable: false),
                    GroupKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErrorRecords_AlphaBeta_AlphaBetaId",
                        column: x => x.AlphaBetaId,
                        principalTable: "AlphaBeta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorRecords_AlphaBetaId",
                table: "ErrorRecords",
                column: "AlphaBetaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErrorRecords");
        }
    }
}
