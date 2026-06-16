using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace API.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeAnalysisSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ErrorRecords_ErrorTypes_ErrorTypeId",
                table: "ErrorRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeGaps_KnowledgeAspects_KnowledgeAspectId",
                table: "KnowledgeGaps");

            migrationBuilder.DropIndex(
                name: "IX_ErrorTypeAspects_ErrorTypeId",
                table: "ErrorTypeAspects");

            migrationBuilder.DropIndex(
                name: "IX_CausalErrorRules_TaskType_SourceErrorCode_TargetErrorCode_R~",
                table: "CausalErrorRules");

            migrationBuilder.AddColumn<int>(
                name: "LevelId",
                table: "KnowledgeGaps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TaskTypeId",
                table: "KnowledgeGaps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TrendId",
                table: "KnowledgeGaps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TopicId",
                table: "KnowledgeAspects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaskTypeId",
                table: "ErrorRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RelationTypeId",
                table: "CausalErrorRules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SourceErrorTypeId",
                table: "CausalErrorRules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TargetErrorTypeId",
                table: "CausalErrorRules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TaskTypeId",
                table: "CausalErrorRules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RelationTypeId",
                table: "CausalErrorLinks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StatusId",
                table: "AnalysisRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TaskTypeId",
                table: "AnalysisRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AnalysisStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CausalRelationTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CausalRelationTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GapLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GapLevels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GapTrends",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GapTrends", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeTopics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeTopics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskTypes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AnalysisStatuses",
                columns: new[] { "Id", "Code", "Name" },
                values: new object[,]
                {
                    { 1, "Started", "Started" },
                    { 2, "Completed", "Completed" },
                    { 3, "Failed", "Failed" }
                });

            migrationBuilder.InsertData(
                table: "CausalRelationTypes",
                columns: new[] { "Id", "Code", "Name" },
                values: new object[,]
                {
                    { 1, "CAUSES", "Causes" },
                    { 2, "EXPLAINS", "Explains" },
                    { 3, "MAY_CAUSE", "May cause" },
                    { 4, "CONTEXT_FOR", "Context for" },
                    { 5, "SUMMARIZES", "Summarizes" }
                });

            migrationBuilder.InsertData(
                table: "GapLevels",
                columns: new[] { "Id", "Code", "Name" },
                values: new object[,]
                {
                    { 1, "Low", "Low" },
                    { 2, "Medium", "Medium" },
                    { 3, "High", "High" },
                    { 4, "Critical", "Critical" }
                });

            migrationBuilder.InsertData(
                table: "GapTrends",
                columns: new[] { "Id", "Code", "Name" },
                values: new object[,]
                {
                    { 1, "Stable", "Stable" },
                    { 2, "Improved", "Improved" },
                    { 3, "Worsened", "Worsened" },
                    { 4, "New", "New" }
                });

            migrationBuilder.InsertData(
                table: "TaskTypes",
                columns: new[] { "Id", "Code", "Name" },
                values: new object[,]
                {
                    { 1, "AlphaBeta", "Alpha-Beta pruning" },
                    { 2, "FifteenPuzzle", "Fifteen puzzle A*" }
                });


            migrationBuilder.Sql(@"
                INSERT INTO ""KnowledgeTopics"" (""Name"")
                SELECT DISTINCT ""TopicName""
                FROM ""KnowledgeAspects""
                WHERE ""TopicName"" IS NOT NULL AND btrim(""TopicName"") <> ''
                  AND NOT EXISTS (SELECT 1 FROM ""KnowledgeTopics"" kt WHERE kt.""Name"" = ""KnowledgeAspects"".""TopicName"");

                INSERT INTO ""ErrorTypes"" (""Code"", ""Name"", ""Description"", ""DefaultSeverity"")
                SELECT DISTINCT ""Code"", ""Code"", NULL, 1.0
                FROM ""ErrorRecords""
                WHERE ""Code"" IS NOT NULL AND btrim(""Code"") <> ''
                  AND NOT EXISTS (SELECT 1 FROM ""ErrorTypes"" et WHERE et.""Code"" = ""ErrorRecords"".""Code"");

                INSERT INTO ""ErrorTypes"" (""Code"", ""Name"", ""Description"", ""DefaultSeverity"")
                SELECT DISTINCT ""SourceErrorCode"", ""SourceErrorCode"", NULL, 1.0
                FROM ""CausalErrorRules""
                WHERE ""SourceErrorCode"" IS NOT NULL AND btrim(""SourceErrorCode"") <> ''
                  AND NOT EXISTS (SELECT 1 FROM ""ErrorTypes"" et WHERE et.""Code"" = ""CausalErrorRules"".""SourceErrorCode"");

                INSERT INTO ""ErrorTypes"" (""Code"", ""Name"", ""Description"", ""DefaultSeverity"")
                SELECT DISTINCT ""TargetErrorCode"", ""TargetErrorCode"", NULL, 1.0
                FROM ""CausalErrorRules""
                WHERE ""TargetErrorCode"" IS NOT NULL AND btrim(""TargetErrorCode"") <> ''
                  AND NOT EXISTS (SELECT 1 FROM ""ErrorTypes"" et WHERE et.""Code"" = ""CausalErrorRules"".""TargetErrorCode"");

                INSERT INTO ""ErrorTypes"" (""Code"", ""Name"", ""Description"", ""DefaultSeverity"")
                SELECT 'UNCLASSIFIED_ERROR', 'Unclassified error', NULL, 1.0
                WHERE NOT EXISTS (SELECT 1 FROM ""ErrorTypes"" et WHERE et.""Code"" = 'UNCLASSIFIED_ERROR');

                UPDATE ""AnalysisRuns"" ar
                SET ""TaskTypeId"" = COALESCE((SELECT tt.""Id"" FROM ""TaskTypes"" tt WHERE tt.""Code"" = ar.""TaskType""), 1),
                    ""StatusId"" = COALESCE((SELECT s.""Id"" FROM ""AnalysisStatuses"" s WHERE s.""Code"" = ar.""Status""), 1);

                UPDATE ""ErrorRecords"" er
                SET ""TaskTypeId"" = COALESCE((SELECT tt.""Id"" FROM ""TaskTypes"" tt WHERE tt.""Code"" = er.""TaskType""), 1),
                    ""ErrorTypeId"" = COALESCE(
                        (SELECT et.""Id"" FROM ""ErrorTypes"" et WHERE et.""Code"" = er.""Code""),
                        er.""ErrorTypeId"",
                        (SELECT et.""Id"" FROM ""ErrorTypes"" et WHERE et.""Code"" = 'UNCLASSIFIED_ERROR'));

                UPDATE ""KnowledgeAspects"" ka
                SET ""TopicId"" = (SELECT kt.""Id"" FROM ""KnowledgeTopics"" kt WHERE kt.""Name"" = ka.""TopicName"")
                WHERE ka.""TopicName"" IS NOT NULL;

                UPDATE ""KnowledgeGaps"" kg
                SET ""TaskTypeId"" = COALESCE((SELECT tt.""Id"" FROM ""TaskTypes"" tt WHERE tt.""Code"" = kg.""TaskType""), 1),
                    ""LevelId"" = COALESCE((SELECT gl.""Id"" FROM ""GapLevels"" gl WHERE gl.""Code"" = kg.""Level""), 1),
                    ""TrendId"" = COALESCE((SELECT gt.""Id"" FROM ""GapTrends"" gt WHERE gt.""Code"" = kg.""Trend""), 1);

                UPDATE ""CausalErrorLinks"" cel
                SET ""RelationTypeId"" = COALESCE((SELECT crt.""Id"" FROM ""CausalRelationTypes"" crt WHERE crt.""Code"" = cel.""RelationType""), 1);

                UPDATE ""CausalErrorRules"" cer
                SET ""TaskTypeId"" = COALESCE((SELECT tt.""Id"" FROM ""TaskTypes"" tt WHERE tt.""Code"" = cer.""TaskType""), 1),
                    ""SourceErrorTypeId"" = COALESCE((SELECT et.""Id"" FROM ""ErrorTypes"" et WHERE et.""Code"" = cer.""SourceErrorCode""), 1),
                    ""TargetErrorTypeId"" = COALESCE((SELECT et.""Id"" FROM ""ErrorTypes"" et WHERE et.""Code"" = cer.""TargetErrorCode""), 1),
                    ""RelationTypeId"" = COALESCE((SELECT crt.""Id"" FROM ""CausalRelationTypes"" crt WHERE crt.""Code"" = cer.""RelationType""), 1);
            ");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "KnowledgeGaps");

            migrationBuilder.DropColumn(
                name: "TaskType",
                table: "KnowledgeGaps");

            migrationBuilder.DropColumn(
                name: "Trend",
                table: "KnowledgeGaps");

            migrationBuilder.DropColumn(
                name: "TopicName",
                table: "KnowledgeAspects");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "ErrorRecords");

            migrationBuilder.AlterColumn<int>(
                name: "ErrorTypeId",
                table: "ErrorRecords",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "TaskType",
                table: "ErrorRecords");

            migrationBuilder.DropColumn(
                name: "RelationType",
                table: "CausalErrorRules");

            migrationBuilder.DropColumn(
                name: "SourceErrorCode",
                table: "CausalErrorRules");

            migrationBuilder.DropColumn(
                name: "TargetErrorCode",
                table: "CausalErrorRules");

            migrationBuilder.DropColumn(
                name: "TaskType",
                table: "CausalErrorRules");

            migrationBuilder.DropColumn(
                name: "RelationType",
                table: "CausalErrorLinks");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AnalysisRuns");

            migrationBuilder.DropColumn(
                name: "TaskType",
                table: "AnalysisRuns");
            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeGaps_LevelId",
                table: "KnowledgeGaps",
                column: "LevelId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeGaps_TaskTypeId",
                table: "KnowledgeGaps",
                column: "TaskTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeGaps_TrendId",
                table: "KnowledgeGaps",
                column: "TrendId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeAspects_TopicId",
                table: "KnowledgeAspects",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorTypes_Code",
                table: "ErrorTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorTypeAspects_ErrorTypeId_KnowledgeAspectId",
                table: "ErrorTypeAspects",
                columns: new[] { "ErrorTypeId", "KnowledgeAspectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorRecords_TaskTypeId",
                table: "ErrorRecords",
                column: "TaskTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CausalErrorRules_RelationTypeId",
                table: "CausalErrorRules",
                column: "RelationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CausalErrorRules_SourceErrorTypeId",
                table: "CausalErrorRules",
                column: "SourceErrorTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CausalErrorRules_TargetErrorTypeId",
                table: "CausalErrorRules",
                column: "TargetErrorTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CausalErrorRules_TaskTypeId_SourceErrorTypeId_TargetErrorTy~",
                table: "CausalErrorRules",
                columns: new[] { "TaskTypeId", "SourceErrorTypeId", "TargetErrorTypeId", "RelationTypeId", "SameNodeRequired", "SameRootBranchRequired" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CausalErrorLinks_RelationTypeId",
                table: "CausalErrorLinks",
                column: "RelationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisRuns_StatusId",
                table: "AnalysisRuns",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisRuns_TaskTypeId",
                table: "AnalysisRuns",
                column: "TaskTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisRuns_UserId",
                table: "AnalysisRuns",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisStatuses_Code",
                table: "AnalysisStatuses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CausalRelationTypes_Code",
                table: "CausalRelationTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GapLevels_Code",
                table: "GapLevels",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GapTrends_Code",
                table: "GapTrends",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeTopics_Name",
                table: "KnowledgeTopics",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskTypes_Code",
                table: "TaskTypes",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AnalysisRuns_AnalysisStatuses_StatusId",
                table: "AnalysisRuns",
                column: "StatusId",
                principalTable: "AnalysisStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnalysisRuns_AspNetUsers_UserId",
                table: "AnalysisRuns",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AnalysisRuns_TaskTypes_TaskTypeId",
                table: "AnalysisRuns",
                column: "TaskTypeId",
                principalTable: "TaskTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CausalErrorLinks_CausalRelationTypes_RelationTypeId",
                table: "CausalErrorLinks",
                column: "RelationTypeId",
                principalTable: "CausalRelationTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CausalErrorRules_CausalRelationTypes_RelationTypeId",
                table: "CausalErrorRules",
                column: "RelationTypeId",
                principalTable: "CausalRelationTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CausalErrorRules_ErrorTypes_SourceErrorTypeId",
                table: "CausalErrorRules",
                column: "SourceErrorTypeId",
                principalTable: "ErrorTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CausalErrorRules_ErrorTypes_TargetErrorTypeId",
                table: "CausalErrorRules",
                column: "TargetErrorTypeId",
                principalTable: "ErrorTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CausalErrorRules_TaskTypes_TaskTypeId",
                table: "CausalErrorRules",
                column: "TaskTypeId",
                principalTable: "TaskTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ErrorRecords_ErrorTypes_ErrorTypeId",
                table: "ErrorRecords",
                column: "ErrorTypeId",
                principalTable: "ErrorTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ErrorRecords_TaskTypes_TaskTypeId",
                table: "ErrorRecords",
                column: "TaskTypeId",
                principalTable: "TaskTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeAspects_KnowledgeTopics_TopicId",
                table: "KnowledgeAspects",
                column: "TopicId",
                principalTable: "KnowledgeTopics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeGaps_GapLevels_LevelId",
                table: "KnowledgeGaps",
                column: "LevelId",
                principalTable: "GapLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeGaps_GapTrends_TrendId",
                table: "KnowledgeGaps",
                column: "TrendId",
                principalTable: "GapTrends",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeGaps_KnowledgeAspects_KnowledgeAspectId",
                table: "KnowledgeGaps",
                column: "KnowledgeAspectId",
                principalTable: "KnowledgeAspects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeGaps_TaskTypes_TaskTypeId",
                table: "KnowledgeGaps",
                column: "TaskTypeId",
                principalTable: "TaskTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnalysisRuns_AnalysisStatuses_StatusId",
                table: "AnalysisRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_AnalysisRuns_AspNetUsers_UserId",
                table: "AnalysisRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_AnalysisRuns_TaskTypes_TaskTypeId",
                table: "AnalysisRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_CausalErrorLinks_CausalRelationTypes_RelationTypeId",
                table: "CausalErrorLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_CausalErrorRules_CausalRelationTypes_RelationTypeId",
                table: "CausalErrorRules");

            migrationBuilder.DropForeignKey(
                name: "FK_CausalErrorRules_ErrorTypes_SourceErrorTypeId",
                table: "CausalErrorRules");

            migrationBuilder.DropForeignKey(
                name: "FK_CausalErrorRules_ErrorTypes_TargetErrorTypeId",
                table: "CausalErrorRules");

            migrationBuilder.DropForeignKey(
                name: "FK_CausalErrorRules_TaskTypes_TaskTypeId",
                table: "CausalErrorRules");

            migrationBuilder.DropForeignKey(
                name: "FK_ErrorRecords_ErrorTypes_ErrorTypeId",
                table: "ErrorRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_ErrorRecords_TaskTypes_TaskTypeId",
                table: "ErrorRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeAspects_KnowledgeTopics_TopicId",
                table: "KnowledgeAspects");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeGaps_GapLevels_LevelId",
                table: "KnowledgeGaps");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeGaps_GapTrends_TrendId",
                table: "KnowledgeGaps");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeGaps_KnowledgeAspects_KnowledgeAspectId",
                table: "KnowledgeGaps");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeGaps_TaskTypes_TaskTypeId",
                table: "KnowledgeGaps");

            migrationBuilder.DropTable(
                name: "AnalysisStatuses");

            migrationBuilder.DropTable(
                name: "CausalRelationTypes");

            migrationBuilder.DropTable(
                name: "GapLevels");

            migrationBuilder.DropTable(
                name: "GapTrends");

            migrationBuilder.DropTable(
                name: "KnowledgeTopics");

            migrationBuilder.DropTable(
                name: "TaskTypes");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeGaps_LevelId",
                table: "KnowledgeGaps");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeGaps_TaskTypeId",
                table: "KnowledgeGaps");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeGaps_TrendId",
                table: "KnowledgeGaps");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeAspects_TopicId",
                table: "KnowledgeAspects");

            migrationBuilder.DropIndex(
                name: "IX_ErrorTypes_Code",
                table: "ErrorTypes");

            migrationBuilder.DropIndex(
                name: "IX_ErrorTypeAspects_ErrorTypeId_KnowledgeAspectId",
                table: "ErrorTypeAspects");

            migrationBuilder.DropIndex(
                name: "IX_ErrorRecords_TaskTypeId",
                table: "ErrorRecords");

            migrationBuilder.DropIndex(
                name: "IX_CausalErrorRules_RelationTypeId",
                table: "CausalErrorRules");

            migrationBuilder.DropIndex(
                name: "IX_CausalErrorRules_SourceErrorTypeId",
                table: "CausalErrorRules");

            migrationBuilder.DropIndex(
                name: "IX_CausalErrorRules_TargetErrorTypeId",
                table: "CausalErrorRules");

            migrationBuilder.DropIndex(
                name: "IX_CausalErrorRules_TaskTypeId_SourceErrorTypeId_TargetErrorTy~",
                table: "CausalErrorRules");

            migrationBuilder.DropIndex(
                name: "IX_CausalErrorLinks_RelationTypeId",
                table: "CausalErrorLinks");

            migrationBuilder.DropIndex(
                name: "IX_AnalysisRuns_StatusId",
                table: "AnalysisRuns");

            migrationBuilder.DropIndex(
                name: "IX_AnalysisRuns_TaskTypeId",
                table: "AnalysisRuns");

            migrationBuilder.DropIndex(
                name: "IX_AnalysisRuns_UserId",
                table: "AnalysisRuns");

            migrationBuilder.DropColumn(
                name: "LevelId",
                table: "KnowledgeGaps");

            migrationBuilder.DropColumn(
                name: "TaskTypeId",
                table: "KnowledgeGaps");

            migrationBuilder.DropColumn(
                name: "TrendId",
                table: "KnowledgeGaps");

            migrationBuilder.DropColumn(
                name: "TopicId",
                table: "KnowledgeAspects");

            migrationBuilder.DropColumn(
                name: "TaskTypeId",
                table: "ErrorRecords");

            migrationBuilder.DropColumn(
                name: "RelationTypeId",
                table: "CausalErrorRules");

            migrationBuilder.DropColumn(
                name: "SourceErrorTypeId",
                table: "CausalErrorRules");

            migrationBuilder.DropColumn(
                name: "TargetErrorTypeId",
                table: "CausalErrorRules");

            migrationBuilder.DropColumn(
                name: "TaskTypeId",
                table: "CausalErrorRules");

            migrationBuilder.DropColumn(
                name: "RelationTypeId",
                table: "CausalErrorLinks");

            migrationBuilder.DropColumn(
                name: "StatusId",
                table: "AnalysisRuns");

            migrationBuilder.DropColumn(
                name: "TaskTypeId",
                table: "AnalysisRuns");

            migrationBuilder.AddColumn<string>(
                name: "Level",
                table: "KnowledgeGaps",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TaskType",
                table: "KnowledgeGaps",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Trend",
                table: "KnowledgeGaps",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TopicName",
                table: "KnowledgeAspects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "ErrorRecords",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TaskType",
                table: "ErrorRecords",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RelationType",
                table: "CausalErrorRules",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceErrorCode",
                table: "CausalErrorRules",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TargetErrorCode",
                table: "CausalErrorRules",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TaskType",
                table: "CausalErrorRules",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RelationType",
                table: "CausalErrorLinks",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "AnalysisRuns",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TaskType",
                table: "AnalysisRuns",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorTypeAspects_ErrorTypeId",
                table: "ErrorTypeAspects",
                column: "ErrorTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CausalErrorRules_TaskType_SourceErrorCode_TargetErrorCode_R~",
                table: "CausalErrorRules",
                columns: new[] { "TaskType", "SourceErrorCode", "TargetErrorCode", "RelationType", "SameNodeRequired", "SameRootBranchRequired" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ErrorRecords_ErrorTypes_ErrorTypeId",
                table: "ErrorRecords",
                column: "ErrorTypeId",
                principalTable: "ErrorTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeGaps_KnowledgeAspects_KnowledgeAspectId",
                table: "KnowledgeGaps",
                column: "KnowledgeAspectId",
                principalTable: "KnowledgeAspects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
