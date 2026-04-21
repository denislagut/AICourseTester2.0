using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using AICourseTester.Models;

namespace AICourseTester.Data
{
    public class MainDbContext : IdentityDbContext<ApplicationUser>
    {
        public MainDbContext(DbContextOptions<MainDbContext> options) : base(options) { }

        public DbSet<FifteenPuzzle> Fifteens { get; set; } = null!;
        public DbSet<AlphaBeta> AlphaBeta { get; set; } = null!;
        public DbSet<Group> Groups { get; set; } = null!;
        public DbSet<UserGroups> UserGroups { get; set; } = null!;

        public DbSet<ErrorRecord> ErrorRecords { get; set; }

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

        }
    }
}
