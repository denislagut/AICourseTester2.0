using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AICourseTester.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeGaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KnowledgeGaps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    AlphaBetaId = table.Column<int>(type: "integer", nullable: true),
                    KnowledgeAspectId = table.Column<int>(type: "integer", nullable: false),
                    ErrorCount = table.Column<int>(type: "integer", nullable: false),
                    TotalWeight = table.Column<double>(type: "double precision", nullable: false),
                    AverageSeverity = table.Column<double>(type: "double precision", nullable: false),
                    GapScore = table.Column<double>(type: "double precision", nullable: false),
                    Level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeGaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeGaps_AlphaBeta_AlphaBetaId",
                        column: x => x.AlphaBetaId,
                        principalTable: "AlphaBeta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KnowledgeGaps_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KnowledgeGaps_KnowledgeAspects_KnowledgeAspectId",
                        column: x => x.KnowledgeAspectId,
                        principalTable: "KnowledgeAspects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeGaps_AlphaBetaId",
                table: "KnowledgeGaps",
                column: "AlphaBetaId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeGaps_KnowledgeAspectId",
                table: "KnowledgeGaps",
                column: "KnowledgeAspectId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeGaps_UserId",
                table: "KnowledgeGaps",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnowledgeGaps");
        }
    }
}
