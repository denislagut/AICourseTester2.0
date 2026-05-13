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

		public DbSet<CausalErrorLink> CausalErrorLinks { get; set; }
		public DbSet<ErrorRecord> ErrorRecords { get; set; }
		public DbSet<ErrorType> ErrorTypes { get; set; }
		public DbSet<KnowledgeAspect> KnowledgeAspects { get; set; }
		public DbSet<ErrorTypeAspect> ErrorTypeAspects { get; set; }
		public DbSet<KnowledgeGap> KnowledgeGaps { get; set; }

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
				.OnDelete(DeleteBehavior.SetNull);

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

			modelBuilder.Entity<KnowledgeGap>()
	            .HasOne(g => g.User)
	            .WithMany()
	            .HasForeignKey(g => g.UserId)
	            .OnDelete(DeleteBehavior.Cascade);
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
				.OnDelete(DeleteBehavior.Cascade);

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
		}
    }
}
