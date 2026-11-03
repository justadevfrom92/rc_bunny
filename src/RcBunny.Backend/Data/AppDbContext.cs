using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RcBunny.Data.Entities;

namespace RcBunny.Data;

/// <summary>
/// The one database both projects use. The Backend owns this file (and migrations / seeding);
/// the Frontend compiles the same source files in (see RcBunny.Frontend.csproj) and only reads/writes rows.
/// Table and column names are snake_case so they match the ebunny (Laravel) schema.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<Market> Markets => Set<Market>();
    public DbSet<ZipRange> ZipRanges => Set<ZipRange>();
    public DbSet<TdspFee> TdspFees => Set<TdspFee>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanGroup> PlanGroups => Set<PlanGroup>();
    public DbSet<PlanGroupPlan> PlanGroupPlans => Set<PlanGroupPlan>();
    public DbSet<Rate> Rates => Set<Rate>();
    public DbSet<TermDiscount> TermDiscounts => Set<TermDiscount>();
    public DbSet<TermEtf> TermEtfs => Set<TermEtf>();
    public DbSet<ByopProduct> ByopProducts => Set<ByopProduct>();

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // SQLite (local dev) can't sort or sum decimals; store them as REAL there. SQL Server keeps decimal(12,4).
        if (Database.IsSqlite())
            b.Properties<decimal>().HaveConversion<double>();
        else
            b.Properties<decimal>().HavePrecision(12, 4);
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Role>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
            JsonList(e.Property(x => x.Perms));
        });
        m.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.EmployeeId).IsUnique();
            e.HasIndex(x => x.Company);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.EmployeeId).HasMaxLength(20);
            e.Property(x => x.Company).HasMaxLength(120);
            e.Property(x => x.PhoneExt).HasMaxLength(10);
            e.HasOne(x => x.Role).WithMany(r => r.Users).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.NoAction);
            JsonList(e.Property(x => x.ExtraPerms));
            JsonList(e.Property(x => x.DeniedPerms));
        });
        m.Entity<Bookmark>(e =>
        {
            e.HasKey(x => new { x.UserId, x.CustomerId });
            e.HasOne(x => x.User).WithMany(u => u.Bookmarks).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        });
        m.Entity<AuditEntry>(e =>
        {
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.UserId);
            e.Property(x => x.Action).HasMaxLength(60);
        });

        m.Entity<Market>(e => e.HasIndex(x => x.Name).IsUnique());
        m.Entity<TdspFee>(e => e.HasIndex(x => new { x.MarketId, x.EffectiveOn }));
        m.Entity<Plan>(e =>
        {
            e.HasIndex(x => x.Internal).IsUnique();
            e.Property(x => x.Type).HasMaxLength(8);
            JsonList(e.Property(x => x.Tags));
            JsonList(e.Property(x => x.Perks));
        });
        m.Entity<PlanGroup>(e => e.HasIndex(x => x.Slug).IsUnique());
        m.Entity<PlanGroupPlan>(e =>
        {
            e.ToTable("plan_group_plan");
            e.HasKey(x => new { x.PlanGroupId, x.PlanId });
            e.HasOne(x => x.PlanGroup).WithMany(g => g.Plans).HasForeignKey(x => x.PlanGroupId);
            e.HasOne(x => x.Plan).WithMany(p => p.Groups).HasForeignKey(x => x.PlanId);
        });
        m.Entity<Rate>(e => e.HasIndex(x => new { x.PlanId, x.MarketId, x.EffectiveOn }));
        m.Entity<TermDiscount>(e => e.HasIndex(x => new { x.Term, x.Region }).IsUnique());
        m.Entity<TermEtf>(e =>
        {
            e.HasKey(x => x.Term);
            e.Property(x => x.Term).ValueGeneratedNever();
        });
        m.Entity<ByopProduct>(e => e.HasIndex(x => x.Key).IsUnique());

        m.Entity<Customer>(e =>
        {
            e.HasIndex(x => x.Account).IsUnique();
            e.HasIndex(x => x.Ticket).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Exception);
            e.HasIndex(x => x.Esiid);
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Zip).HasMaxLength(10);
            e.HasOne(x => x.Market).WithMany().HasForeignKey(x => x.MarketId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.SetNull);
        });
        m.Entity<Payment>(e =>
        {
            e.HasIndex(x => x.Reference).IsUnique();
            e.HasOne(x => x.Customer).WithMany(c => c.Payments).HasForeignKey(x => x.CustomerId);
        });
        m.Entity<Bill>(e =>
        {
            e.HasIndex(x => x.Reference).IsUnique();
            e.HasOne(x => x.Customer).WithMany(c => c.Bills).HasForeignKey(x => x.CustomerId);
        });
        m.Entity<Note>(e => e.HasOne(x => x.Customer).WithMany(c => c.Notes).HasForeignKey(x => x.CustomerId));

        SnakeCaseNames(m);
    }

    /// <summary>List&lt;string&gt; stored as a JSON array (Laravel's "array" cast).</summary>
    private static void JsonList(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<List<string>> p) => JsonListCore(p);

    private static void JsonListCore(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<List<string>> p)
    {
        p.HasConversion(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => string.IsNullOrEmpty(v) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>(),
            new ValueComparer<List<string>>(
                (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
                v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
                v => v.ToList()));
    }

    private static void SnakeCaseNames(ModelBuilder m)
    {
        foreach (var entity in m.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table != null && table != "plan_group_plan")
                entity.SetTableName(Snake(table));
            foreach (var prop in entity.GetProperties())
                prop.SetColumnName(Snake(prop.Name));
            foreach (var key in entity.GetKeys())
                key.SetName(Snake(key.GetName() ?? ""));
            foreach (var fk in entity.GetForeignKeys())
                fk.SetConstraintName(Snake(fk.GetConstraintName() ?? ""));
            foreach (var ix in entity.GetIndexes())
                ix.SetDatabaseName(Snake(ix.GetDatabaseName() ?? ""));
        }
    }

    public static string Snake(string name)
    {
        var sb = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && name[i - 1] != '_' && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Keeps UpdatedAt current on every save.</summary>
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Modified))
        {
            var prop = entry.Metadata.FindProperty("UpdatedAt");
            if (prop != null) entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(ct);
    }
}
