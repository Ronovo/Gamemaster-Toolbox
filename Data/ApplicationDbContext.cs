using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using GM_Toolbox.Models;

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

            // Every query sees only the signed-in account's own items.
            builder.Entity<GameSystem>().HasQueryFilter(g => g.OwnerId == CurrentUserId);
            builder.Entity<Oracle>().HasQueryFilter(o => o.OwnerId == CurrentUserId);
            builder.Entity<Flow>().HasQueryFilter(f => f.OwnerId == CurrentUserId);
            builder.Entity<Sheet>().HasQueryFilter(s => s.OwnerId == CurrentUserId);

            OwnedByAccount<GameSystem>(builder);
            OwnedByAccount<Oracle>(builder);
            OwnedByAccount<Flow>(builder);
            OwnedByAccount<Sheet>(builder);

            builder.Entity<Oracle>()
                .Property(o => o.OracleSubtype)
                .HasConversion<string>();

            builder.Entity<Oracle>()
                .HasOne(o => o.GameSystem)
                .WithMany()
                .HasForeignKey(o => o.GameSystemID)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<GameSystem>()
                .Property(g => g.Name)
                .UseCollation("NOCASE");

            // Unique per owner, so two accounts can each have a system with the same name.
            builder.Entity<GameSystem>()
                .HasIndex(g => new { g.OwnerId, g.Name })
                .IsUnique();

            builder.Entity<Flow>()
                .HasOne(f => f.GameSystem)
                .WithMany()
                .HasForeignKey(f => f.GameSystemID)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<FlowStep>()
                .HasOne(s => s.Flow)
                .WithMany(f => f.Steps)
                .HasForeignKey(s => s.FlowID)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<FlowStep>()
                .HasOne(s => s.Oracle)
                .WithMany()
                .HasForeignKey(s => s.OracleID)
                .OnDelete(DeleteBehavior.NoAction);

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
                .OnDelete(DeleteBehavior.NoAction);

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
                .OnDelete(DeleteBehavior.NoAction);

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
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Sheet>()
                .HasIndex(s => new { s.OwnerId, s.FileName });

            builder.Entity<SheetVersion>()
                .HasOne(v => v.Sheet)
                .WithMany(s => s.Versions)
                .HasForeignKey(v => v.SheetID)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SheetVersion>()
                .HasIndex(v => new { v.SheetID, v.Version })
                .IsUnique();
        }

        // Deleting the account deletes everything it owns. Links between owned items are NO ACTION rather than
        // RESTRICT, because SQLite checks RESTRICT row by row partway through that cascade.
        private static void OwnedByAccount<T>(ModelBuilder builder) where T : class, IOwned
        {
            builder.Entity<T>()
                .HasOne<IdentityUser>()
                .WithMany()
                .HasForeignKey(nameof(IOwned.OwnerId))
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
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