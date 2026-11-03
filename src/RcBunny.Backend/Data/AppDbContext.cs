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
    public DbSet<TextMessage> TextMessages => Set<TextMessage>();
    public DbSet<PhoneCall> PhoneCalls => Set<PhoneCall>();
    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductOrder> ProductOrders => Set<ProductOrder>();
    public DbSet<ErcotTransaction> ErcotTransactions => Set<ErcotTransaction>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<RangerAgent> RangerAgents => Set<RangerAgent>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<HeartbeatControl> HeartbeatControls => Set<HeartbeatControl>();
    public DbSet<HealthCheck> HealthChecks => Set<HealthCheck>();
    public DbSet<JobRun> JobRuns => Set<JobRun>();

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
        m.Entity<Customer>().HasIndex(c => c.Esiid);
        m.Entity<Product>().HasIndex(p => p.Sku).IsUnique();
        m.Entity<RangerAgent>().HasIndex(a => a.UserId).IsUnique();
        m.Entity<HeartbeatControl>().HasIndex(h => h.Key).IsUnique();
    }
}
