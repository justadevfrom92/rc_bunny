using Microsoft.EntityFrameworkCore;

namespace RcBunny.Data;

/// <summary>
/// The one database both projects use. The Backend owns this file (and migrations / seeding);
/// the Frontend compiles the same source files in (see RcBunny.Frontend.csproj) and only reads/writes rows.
/// Every table and column matches the ebunny (Laravel) schema; the mapping is generated into
/// AppDbContext.Generated.cs and Entities/Generated.cs by tools/schema (re-run it after changing ebunny's migrations).
/// </summary>
public partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // SQLite (local dev) can't sort or sum decimals; store them as REAL there. SQL Server keeps decimal(p,s).
        if (Database.IsSqlite())
            b.Properties<decimal>().HaveConversion<double>();
        else
            b.Properties<decimal>().HavePrecision(12, 4);
    }

    protected override void OnModelCreating(ModelBuilder m) => ConfigureGenerated(m);

    /// <summary>Keeps updated_at current on every save.</summary>
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Modified))
            if (entry.Metadata.FindProperty("UpdatedAt") != null)
                entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
        return base.SaveChangesAsync(ct);
    }
}
