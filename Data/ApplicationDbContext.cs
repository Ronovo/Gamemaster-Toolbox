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

            // Game system names are case-insensitive. SQLite compares TEXT binary, so without
            // NOCASE "Cyberpunk Red" and "cyberpunk red" become two rows — past the unique
            // index below and past GameSystemsController's duplicate check — while
            // OracleImporter's OrdinalIgnoreCase lookup still treats them as one.
            builder.Entity<GameSystem>()
                .Property(g => g.Name)
                .UseCollation("NOCASE");

            builder.Entity<GameSystem>()
                .HasIndex(g => g.Name)
                .IsUnique();

            builder.Entity<GameSystem>()
                .Property(g => g.PrimaryOracleType)
                .HasConversion<string>();

            // No HasData seed. A fresh database starts with no game systems: this is a
            // distributed tool, and one instance's idea of which systems matter is not
            // another's. Create your own, or import a file that names them.
        }

        public DbSet<Oracle> Oracles { get; set; }
        public DbSet<OracleEntry> OracleEntries { get; set; }
        public DbSet<GameSystem> GameSystems { get; set; }
    }
}