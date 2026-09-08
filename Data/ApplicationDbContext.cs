using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using GM_Toolbox.Models;

namespace GM_Toolbox.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Oracle>()
                .Property(o => o.OracleSubtype)
                .HasConversion<string>();

            builder.Entity<Oracle>()
                .HasOne(o => o.GameSystem)
                .WithMany()
                .HasForeignKey(o => o.GameSystemID)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<GameSystem>()
                .Property(g => g.Name)
                .UseCollation("NOCASE");

            builder.Entity<GameSystem>()
                .HasIndex(g => g.Name)
                .IsUnique();

            builder.Entity<Flow>()
                .HasOne(f => f.GameSystem)
                .WithMany()
                .HasForeignKey(f => f.GameSystemID)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<FlowStep>()
                .HasOne(s => s.Flow)
                .WithMany(f => f.Steps)
                .HasForeignKey(s => s.FlowID)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<FlowStep>()
                .HasOne(s => s.Oracle)
                .WithMany()
                .HasForeignKey(s => s.OracleID)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<FlowStep>()
                .Property(s => s.Kind)
                .HasConversion<string>();

            builder.Entity<FlowChoiceOption>()
                .HasOne(o => o.FlowStep)
                .WithMany(s => s.Options)
                .HasForeignKey(o => o.FlowStepID)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<FlowChoiceOption>()
                .HasOne(o => o.Oracle)
                .WithMany()
                .HasForeignKey(o => o.OracleID)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<FlowBranch>()
                .HasOne(b => b.FlowStep)
                .WithMany(s => s.Branches)
                .HasForeignKey(b => b.FlowStepID)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<FlowBranch>()
                .HasOne(b => b.OracleEntry)
                .WithMany()
                .HasForeignKey(b => b.OracleEntryID)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<FlowBranch>()
                .HasIndex(b => new { b.FlowStepID, b.OracleEntryID })
                .IsUnique();

            // No HasData seed. A fresh database starts with no game systems: this is a
            // distributed tool, and one instance's idea of which systems matter is not
            // another's. Create your own, or import a file that names them.
        }

        public DbSet<Oracle> Oracles { get; set; }
        public DbSet<OracleEntry> OracleEntries { get; set; }
        public DbSet<GameSystem> GameSystems { get; set; }
        public DbSet<Flow> Flows { get; set; }
        public DbSet<FlowStep> FlowSteps { get; set; }
        public DbSet<FlowBranch> FlowBranches { get; set; }
        public DbSet<FlowChoiceOption> FlowChoiceOptions { get; set; }
    }
}