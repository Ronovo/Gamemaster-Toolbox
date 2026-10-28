using System.Security.Claims;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;

namespace GM_Toolbox.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options,
        IHttpContextAccessor? httpContextAccessor = null) : IdentityDbContext(options)
    {
        // The signed-in account, or null for a visitor and for work outside a request, like startup.
        public string? CurrentUserId => httpContextAccessor?.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            await new OwnershipRules(this).CheckAsync(cancellationToken);
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Every query sees only what everyone can see plus the signed-in account's own items.
            // IgnoreQueryFilters() is for checks that must count everything, like "is this still in use".
            builder.Entity<Oracle>()
                .HasQueryFilter(o => o.Visibility == Visibility.Everyone || (CurrentUserId != null && o.OwnerId == CurrentUserId));
            builder.Entity<Flow>()
                .HasQueryFilter(f => f.Visibility == Visibility.Everyone || (CurrentUserId != null && f.OwnerId == CurrentUserId));
            builder.Entity<GameSystem>()
                .HasQueryFilter(g => g.Visibility == Visibility.Everyone || (CurrentUserId != null && g.OwnerId == CurrentUserId));

            builder.Entity<Oracle>().Property(o => o.Visibility).HasConversion<string>();
            builder.Entity<Flow>().Property(f => f.Visibility).HasConversion<string>();
            builder.Entity<GameSystem>().Property(g => g.Visibility).HasConversion<string>();

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

            // Unique per owner, so a private game system never blocks someone else's name.
            builder.Entity<GameSystem>()
                .HasIndex(g => new { g.OwnerId, g.Name })
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

            // A RollChosen step names an earlier step. NoAction: FlowConfigurator.RemoveStep
            // clears every reference to a step before it is deleted, so the FK is only here
            // for referential integrity and to let the importer wire steps up by object.
            builder.Entity<FlowStep>()
                .HasOne(s => s.SourceStep)
                .WithMany()
                .HasForeignKey(s => s.SourceStepID)
                .OnDelete(DeleteBehavior.NoAction);

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

            builder.Entity<FlowChoiceOption>()
                .HasOne(o => o.TargetStep)
                .WithMany()
                .HasForeignKey(o => o.TargetStepID)
                .OnDelete(DeleteBehavior.NoAction);

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
                .HasOne(b => b.Oracle)
                .WithMany()
                .HasForeignKey(b => b.OracleID)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<FlowBranch>()
                .HasOne(b => b.TargetStep)
                .WithMany()
                .HasForeignKey(b => b.TargetStepID)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<FlowBranch>()
                .HasIndex(b => new { b.FlowStepID, b.OracleEntryID })
                .IsUnique();

            builder.Entity<Sheet>()
                .HasOne(s => s.GameSystem)
                .WithMany()
                .HasForeignKey(s => s.GameSystemID)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Sheet>()
                .HasIndex(s => s.FileName);

            builder.Entity<SheetVersion>()
                .HasOne(v => v.Sheet)
                .WithMany(s => s.Versions)
                .HasForeignKey(v => v.SheetID)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SheetVersion>()
                .HasIndex(v => new { v.SheetID, v.Version })
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
        public DbSet<Sheet> Sheets { get; set; }
        public DbSet<SheetVersion> SheetVersions { get; set; }
    }
}