using Microsoft.EntityFrameworkCore;

namespace RcBunny.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Market> Markets => Set<Market>();
    public DbSet<ZipRange> ZipRanges => Set<ZipRange>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Rate> Rates => Set<Rate>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // SQLite can't sum or sort decimals, so store money as REAL there. SQL Server keeps real decimals.
        if (Database.IsSqlite()) b.Properties<decimal>().HaveConversion<double>();
        else b.Properties<decimal>().HavePrecision(12, 4);
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<User>().HasIndex(u => u.Email).IsUnique();
        m.Entity<Customer>().HasIndex(c => c.Account).IsUnique();
        m.Entity<Customer>().HasIndex(c => c.Username).IsUnique();
    }
}
