using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

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

    protected override void OnModelCreating(ModelBuilder m)
    {
        ConfigureGenerated(m);
        if (Database.IsSqlServer()) LimitCascadesForSqlServer(m);
    }

    /// <summary>
    /// ebunny's MySQL schema cascades freely; SQL Server refuses a delete rule that could reach a table by two routes
    /// or loop back. Keep as many database cascades as possible (customers' and users' first) and let EF do the rest
    /// (ClientCascade / ClientSetNull) for rows it has loaded.
    /// </summary>
    private static void LimitCascadesForSqlServer(ModelBuilder m)
    {
        var down = new Dictionary<IMutableEntityType, HashSet<IMutableEntityType>>();
        HashSet<IMutableEntityType> Reach(IMutableEntityType from)
        {
            var seen = new HashSet<IMutableEntityType>();
            var stack = new Stack<IMutableEntityType>([from]);
            while (stack.TryPop(out var t))
                foreach (var d in down.GetValueOrDefault(t) ?? [])
                    if (seen.Add(d)) stack.Push(d);
            return seen;
        }
        static int Rank(IMutableForeignKey fk) => fk.PrincipalEntityType.GetTableName() switch { "customers" => 0, "users" => 1, _ => 2 };
        var fks = m.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys())
            .Where(fk => fk.DeleteBehavior is DeleteBehavior.Cascade or DeleteBehavior.SetNull)
            .OrderBy(Rank).ThenBy(fk => fk.PrincipalEntityType.GetTableName()).ThenBy(fk => fk.DeclaringEntityType.GetTableName())
            .ThenBy(fk => string.Join(",", fk.Properties.Select(p => p.Name))).ToList();
        foreach (var fk in fks)
        {
            var (p, d) = (fk.PrincipalEntityType, fk.DeclaringEntityType);
            var ancestors = m.Model.GetEntityTypes().Where(a => a == p || Reach(a).Contains(p)).ToList();
            var below = Reach(d); below.Add(d);
            var clash = p == d || below.Contains(p) || ancestors.Any(a => Reach(a).Overlaps(below));
            if (clash)
                fk.DeleteBehavior = fk.DeleteBehavior == DeleteBehavior.Cascade ? DeleteBehavior.ClientCascade : DeleteBehavior.ClientSetNull;
            else
                (down.TryGetValue(p, out var set) ? set : down[p] = []).Add(d);
        }
    }

    /// <summary>Keeps updated_at current on every save.</summary>
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Modified))
            if (entry.Metadata.FindProperty("UpdatedAt") != null)
                entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
        return base.SaveChangesAsync(ct);
    }
}
