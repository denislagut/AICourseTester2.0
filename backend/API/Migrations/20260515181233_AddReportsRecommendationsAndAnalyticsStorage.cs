using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace API.Migrations
{
    /// <inheritdoc />
    public partial class AddReportsRecommendationsAndAnalyticsStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalyticsSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScopeType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    GroupId = table.Column<int>(type: "integer", nullable: true),
                    TotalStudents = table.Column<int>(type: "integer", nullable: false),
                    TotalGroups = table.Column<int>(type: "integer", nullable: false),
                    TotalErrors = table.Column<int>(type: "integer", nullable: false),
                    TotalKnowledgeGaps = table.Column<int>(type: "integer", nullable: false),
                    AverageGapScore = table.Column<double>(type: "double precision", nullable: false),
                    HighSeverityErrorsCount = table.Column<int>(type: "integer", nullable: false),
                    TopErrorTypesJson = table.Column<string>(type: "text", nullable: true),
                    TopKnowledgeGapsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalyticsSnapshots_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AnalyticsSnapshots_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GeneratedRecommendations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    GroupId = table.Column<int>(type: "integer", nullable: true),
                    KnowledgeAspectId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TopicName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    GapScore = table.Column<double>(type: "double precision", nullable: false),
                    RelatedErrorCount = table.Column<int>(type: "integer", nullable: false),
                    AffectedStudentsCount = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedRecommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeneratedRecommendations_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GeneratedRecommendations_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GeneratedRecommendations_KnowledgeAspects_KnowledgeAspectId",
                        column: x => x.KnowledgeAspectId,
                        principalTable: "KnowledgeAspects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GeneratedReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReportType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    GroupId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SummaryJson = table.Column<string>(type: "text", nullable: false),
                    RecommendationsJson = table.Column<string>(type: "text", nullable: false),
                    AnalyticsJson = table.Column<string>(type: "text", nullable: false),
                    Format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeneratedReports_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GeneratedReports_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsSnapshots_GroupId",
                table: "AnalyticsSnapshots",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsSnapshots_UserId",
                table: "AnalyticsSnapshots",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedRecommendations_GroupId",
                table: "GeneratedRecommendations",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedRecommendations_KnowledgeAspectId",
                table: "GeneratedRecommendations",
                column: "KnowledgeAspectId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedRecommendations_UserId",
                table: "GeneratedRecommendations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedReports_GroupId",
                table: "GeneratedReports",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedReports_UserId",
                table: "GeneratedReports",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalyticsSnapshots");

            migrationBuilder.DropTable(
                name: "GeneratedRecommendations");

            migrationBuilder.DropTable(
                name: "GeneratedReports");
        }
    }
}
