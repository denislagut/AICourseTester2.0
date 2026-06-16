using AICourseTester.Models;
using AICourseTester.Models.Analysis;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AICourseTester.Data
{
    public class MainDbContext : IdentityDbContext<ApplicationUser>
    {
        public MainDbContext(DbContextOptions<MainDbContext> options) : base(options) { }

        public DbSet<FifteenPuzzle> Fifteens { get; set; } = null!;
        public DbSet<AlphaBeta> AlphaBeta { get; set; } = null!;
        public DbSet<Group> Groups { get; set; } = null!;
        public DbSet<UserGroups> UserGroups { get; set; } = null!;

		public DbSet<AnalysisRun> AnalysisRuns { get; set; }
		public DbSet<TaskType> TaskTypes { get; set; }
		public DbSet<AnalysisStatus> AnalysisStatuses { get; set; }
		public DbSet<GapLevel> GapLevels { get; set; }
		public DbSet<GapTrend> GapTrends { get; set; }
		public DbSet<CausalRelationType> CausalRelationTypes { get; set; }
		public DbSet<KnowledgeTopic> KnowledgeTopics { get; set; }

		public DbSet<CausalErrorLink> CausalErrorLinks { get; set; }
		public DbSet<CausalErrorRule> CausalErrorRules { get; set; }
		public DbSet<ErrorRecord> ErrorRecords { get; set; }
		public DbSet<ErrorType> ErrorTypes { get; set; }
		public DbSet<KnowledgeAspect> KnowledgeAspects { get; set; }
		public DbSet<ErrorTypeAspect> ErrorTypeAspects { get; set; }
		public DbSet<KnowledgeGap> KnowledgeGaps { get; set; }
		public DbSet<GeneratedRecommendation> GeneratedRecommendations { get; set; }
		public DbSet<GeneratedReport> GeneratedReports { get; set; }
		public DbSet<AnalyticsSnapshot> AnalyticsSnapshots { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<FifteenPuzzle>()
                .Property(fp => fp.Dimensions)
                .HasDefaultValue(4);

            modelBuilder.Entity<FifteenPuzzle>()
                .Property(fp => fp.TreeHeight)
                .HasDefaultValue(3);

            modelBuilder.Entity<FifteenPuzzle>()
                .Property(fp => fp.IsSolved)
                .HasDefaultValue(false);

            modelBuilder.Entity<FifteenPuzzle>()
                .Property(fp => fp.Date)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<AlphaBeta>()
                .Property(ab => ab.TreeHeight)
                .HasDefaultValue(3);

            modelBuilder.Entity<AlphaBeta>()
                .Property(ab => ab.Date)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<AlphaBeta>()
                .Property(fp => fp.IsSolved)
                .HasDefaultValue(false);

            modelBuilder.Entity<ApplicationUser>()
                .Ignore(c => c.Email)
                .Ignore(c => c.NormalizedEmail)
                .Ignore(c => c.EmailConfirmed)
                .Ignore(c => c.TwoFactorEnabled)
                .Ignore(c => c.PhoneNumber)
                .Ignore(c => c.PhoneNumberConfirmed);

			modelBuilder.Entity<TaskType>()
				.HasIndex(t => t.Code)
				.IsUnique();
			modelBuilder.Entity<AnalysisStatus>()
				.HasIndex(s => s.Code)
				.IsUnique();
			modelBuilder.Entity<GapLevel>()
				.HasIndex(l => l.Code)
				.IsUnique();
			modelBuilder.Entity<GapTrend>()
				.HasIndex(t => t.Code)
				.IsUnique();
			modelBuilder.Entity<CausalRelationType>()
				.HasIndex(t => t.Code)
				.IsUnique();
			modelBuilder.Entity<KnowledgeTopic>()
				.HasIndex(t => t.Name)
				.IsUnique();
			modelBuilder.Entity<ErrorType>()
				.HasIndex(t => t.Code)
				.IsUnique();

			modelBuilder.Entity<TaskType>().HasData(
				new TaskType { Id = 1, Code = "AlphaBeta", Name = "Alpha-Beta pruning" },
				new TaskType { Id = 2, Code = "FifteenPuzzle", Name = "Fifteen puzzle A*" });
			modelBuilder.Entity<AnalysisStatus>().HasData(
				new AnalysisStatus { Id = 1, Code = "Started", Name = "Started" },
				new AnalysisStatus { Id = 2, Code = "Completed", Name = "Completed" },
				new AnalysisStatus { Id = 3, Code = "Failed", Name = "Failed" });
			modelBuilder.Entity<GapLevel>().HasData(
				new GapLevel { Id = 1, Code = "Low", Name = "Low" },
				new GapLevel { Id = 2, Code = "Medium", Name = "Medium" },
				new GapLevel { Id = 3, Code = "High", Name = "High" },
				new GapLevel { Id = 4, Code = "Critical", Name = "Critical" });
			modelBuilder.Entity<GapTrend>().HasData(
				new GapTrend { Id = 1, Code = "Stable", Name = "Stable" },
				new GapTrend { Id = 2, Code = "Improved", Name = "Improved" },
				new GapTrend { Id = 3, Code = "Worsened", Name = "Worsened" },
				new GapTrend { Id = 4, Code = "New", Name = "New" });
			modelBuilder.Entity<CausalRelationType>().HasData(
				new CausalRelationType { Id = 1, Code = "CAUSES", Name = "Causes" },
				new CausalRelationType { Id = 2, Code = "EXPLAINS", Name = "Explains" },
				new CausalRelationType { Id = 3, Code = "MAY_CAUSE", Name = "May cause" },
				new CausalRelationType { Id = 4, Code = "CONTEXT_FOR", Name = "Context for" },
				new CausalRelationType { Id = 5, Code = "SUMMARIZES", Name = "Summarizes" });

			modelBuilder.Entity<AnalysisRun>()
				.HasOne(r => r.User)
				.WithMany()
				.HasForeignKey(r => r.UserId)
				.OnDelete(DeleteBehavior.Cascade);
			modelBuilder.Entity<AnalysisRun>()
				.HasOne(r => r.TaskTypeRef)
				.WithMany(t => t.AnalysisRuns)
				.HasForeignKey(r => r.TaskTypeId)
				.OnDelete(DeleteBehavior.Restrict);
			modelBuilder.Entity<AnalysisRun>()
				.HasOne(r => r.StatusRef)
				.WithMany(s => s.AnalysisRuns)
				.HasForeignKey(r => r.StatusId)
				.OnDelete(DeleteBehavior.Restrict);
			modelBuilder.Entity<AnalysisRun>()
				.HasMany(r => r.ErrorRecords)
				.WithOne(e => e.AnalysisRun)
				.HasForeignKey(e => e.AnalysisRunId)
				.OnDelete(DeleteBehavior.SetNull);

			modelBuilder.Entity<ErrorRecord>()
				.HasOne(e => e.TaskTypeRef)
				.WithMany(t => t.ErrorRecords)
				.HasForeignKey(e => e.TaskTypeId)
				.OnDelete(DeleteBehavior.Restrict);
			modelBuilder.Entity<ErrorRecord>()
                .HasOne(e => e.AlphaBeta)
                .WithMany(a => a.Errors)
                .HasForeignKey(e => e.AlphaBetaId)
                .OnDelete(DeleteBehavior.Cascade);
			modelBuilder.Entity<ErrorRecord>()
				.HasOne(e => e.FifteenPuzzle)
				.WithMany()
				.HasForeignKey(e => e.FifteenPuzzleId)
				.OnDelete(DeleteBehavior.Cascade);
			modelBuilder.Entity<ErrorRecord>()
				.HasOne(e => e.ErrorType)
				.WithMany(t => t.Errors)
				.HasForeignKey(e => e.ErrorTypeId)
				.IsRequired()
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<ErrorTypeAspect>()
				.HasIndex(eta => new { eta.ErrorTypeId, eta.KnowledgeAspectId })
				.IsUnique();
			modelBuilder.Entity<ErrorTypeAspect>()
				.HasOne(eta => eta.ErrorType)
				.WithMany(et => et.ErrorTypeAspects)
				.HasForeignKey(eta => eta.ErrorTypeId)
				.OnDelete(DeleteBehavior.Cascade);
			modelBuilder.Entity<ErrorTypeAspect>()
				.HasOne(eta => eta.KnowledgeAspect)
				.WithMany(ka => ka.ErrorTypeAspects)
				.HasForeignKey(eta => eta.KnowledgeAspectId)
				.OnDelete(DeleteBehavior.Cascade);

			modelBuilder.Entity<KnowledgeAspect>()
				.HasOne(a => a.Topic)
				.WithMany(t => t.KnowledgeAspects)
				.HasForeignKey(a => a.TopicId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<KnowledgeGap>()
	            .HasOne(g => g.User)
	            .WithMany()
				.HasForeignKey(g => g.UserId)
				.OnDelete(DeleteBehavior.Cascade);
			modelBuilder.Entity<KnowledgeGap>()
				.HasOne(g => g.TaskTypeRef)
				.WithMany(t => t.KnowledgeGaps)
				.HasForeignKey(g => g.TaskTypeId)
				.OnDelete(DeleteBehavior.Restrict);
			modelBuilder.Entity<KnowledgeGap>()
				.HasOne(g => g.AlphaBeta)
				.WithMany()
				.HasForeignKey(g => g.AlphaBetaId)
				.OnDelete(DeleteBehavior.Cascade);
			modelBuilder.Entity<KnowledgeGap>()
				.HasOne(g => g.FifteenPuzzle)
				.WithMany()
				.HasForeignKey(g => g.FifteenPuzzleId)
				.OnDelete(DeleteBehavior.Cascade);
			modelBuilder.Entity<KnowledgeGap>()
				.HasOne(g => g.KnowledgeAspect)
				.WithMany()
				.HasForeignKey(g => g.KnowledgeAspectId)
				.OnDelete(DeleteBehavior.Restrict);
			modelBuilder.Entity<KnowledgeGap>()
				.HasOne(g => g.AnalysisRun)
				.WithMany()
				.HasForeignKey(g => g.AnalysisRunId)
				.OnDelete(DeleteBehavior.SetNull);
			modelBuilder.Entity<KnowledgeGap>()
				.HasOne(g => g.LevelRef)
				.WithMany(l => l.KnowledgeGaps)
				.HasForeignKey(g => g.LevelId)
				.OnDelete(DeleteBehavior.Restrict);
			modelBuilder.Entity<KnowledgeGap>()
				.HasOne(g => g.TrendRef)
				.WithMany(t => t.KnowledgeGaps)
				.HasForeignKey(g => g.TrendId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<GeneratedRecommendation>()
				.HasOne(r => r.User)
				.WithMany()
				.HasForeignKey(r => r.UserId)
				.OnDelete(DeleteBehavior.SetNull);
			modelBuilder.Entity<GeneratedRecommendation>()
				.HasOne(r => r.Group)
				.WithMany()
				.HasForeignKey(r => r.GroupId)
				.OnDelete(DeleteBehavior.SetNull);
			modelBuilder.Entity<GeneratedRecommendation>()
				.HasOne(r => r.KnowledgeAspect)
				.WithMany()
				.HasForeignKey(r => r.KnowledgeAspectId)
				.OnDelete(DeleteBehavior.SetNull);

			modelBuilder.Entity<GeneratedReport>()
				.HasOne(r => r.User)
				.WithMany()
				.HasForeignKey(r => r.UserId)
				.OnDelete(DeleteBehavior.SetNull);
			modelBuilder.Entity<GeneratedReport>()
				.HasOne(r => r.Group)
				.WithMany()
				.HasForeignKey(r => r.GroupId)
				.OnDelete(DeleteBehavior.SetNull);

			modelBuilder.Entity<AnalyticsSnapshot>()
				.HasOne(s => s.User)
				.WithMany()
				.HasForeignKey(s => s.UserId)
				.OnDelete(DeleteBehavior.SetNull);
			modelBuilder.Entity<AnalyticsSnapshot>()
				.HasOne(s => s.Group)
				.WithMany()
				.HasForeignKey(s => s.GroupId)
				.OnDelete(DeleteBehavior.SetNull);

			modelBuilder.Entity<CausalErrorLink>()
				.HasOne(x => x.SourceError)
				.WithMany(x => x.OutgoingLinks)
				.HasForeignKey(x => x.SourceErrorId)
				.OnDelete(DeleteBehavior.Restrict);
			modelBuilder.Entity<CausalErrorLink>()
				.HasOne(x => x.TargetError)
				.WithMany(x => x.IncomingLinks)
				.HasForeignKey(x => x.TargetErrorId)
				.OnDelete(DeleteBehavior.Restrict);
			modelBuilder.Entity<CausalErrorLink>()
				.HasOne(x => x.RelationTypeRef)
				.WithMany(t => t.CausalErrorLinks)
				.HasForeignKey(x => x.RelationTypeId)
				.OnDelete(DeleteBehavior.Restrict);
			modelBuilder.Entity<CausalErrorRule>()
				.HasOne(r => r.TaskTypeRef)
				.WithMany(t => t.CausalErrorRules)
				.HasForeignKey(r => r.TaskTypeId)
				.OnDelete(DeleteBehavior.Restrict);
			modelBuilder.Entity<CausalErrorRule>()
				.HasOne(r => r.SourceErrorType)
				.WithMany()
				.HasForeignKey(r => r.SourceErrorTypeId)
				.OnDelete(DeleteBehavior.Restrict);
			modelBuilder.Entity<CausalErrorRule>()
				.HasOne(r => r.TargetErrorType)
				.WithMany()
				.HasForeignKey(r => r.TargetErrorTypeId)
				.OnDelete(DeleteBehavior.Restrict);
			modelBuilder.Entity<CausalErrorRule>()
				.HasOne(r => r.RelationTypeRef)
				.WithMany(t => t.CausalErrorRules)
				.HasForeignKey(r => r.RelationTypeId)
				.OnDelete(DeleteBehavior.Restrict);
			modelBuilder.Entity<CausalErrorRule>()
				.HasIndex(r => new
				{
					r.TaskTypeId,
					r.SourceErrorTypeId,
					r.TargetErrorTypeId,
					r.RelationTypeId,
					r.SameNodeRequired,
					r.SameRootBranchRequired
				})
				.IsUnique();
		}
    }
}
