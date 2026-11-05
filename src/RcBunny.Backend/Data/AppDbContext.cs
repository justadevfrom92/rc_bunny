using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

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
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<PlanGroup> PlanGroups => Set<PlanGroup>();
    public DbSet<TermDiscount> TermDiscounts => Set<TermDiscount>();
    public DbSet<TermEtf> TermEtfs => Set<TermEtf>();
    public DbSet<ByopProduct> ByopProducts => Set<ByopProduct>();
    public DbSet<HistoryEntry> History => Set<HistoryEntry>();

    // Corral
    public DbSet<CustomerFlag> CustomerFlags => Set<CustomerFlag>();
    public DbSet<CustomerProduct> CustomerProducts => Set<CustomerProduct>();
    public DbSet<PlanTerm> PlanTerms => Set<PlanTerm>();
    public DbSet<ServiceAddress> ServiceAddresses => Set<ServiceAddress>();
    public DbSet<QueueLog> QueueLogs => Set<QueueLog>();
    public DbSet<CustomerFile> CustomerFiles => Set<CustomerFile>();
    public DbSet<ContactLog> ContactLogs => Set<ContactLog>();
    public DbSet<PhoneCall> PhoneCalls => Set<PhoneCall>();
    public DbSet<ApiLog> ApiLogs => Set<ApiLog>();
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<ErcotTransaction> ErcotTransactions => Set<ErcotTransaction>();
    public DbSet<ShopProduct> ShopProducts => Set<ShopProduct>();
    public DbSet<ShopOrder> ShopOrders => Set<ShopOrder>();
    public DbSet<CrmPartner> CrmPartners => Set<CrmPartner>();
    public DbSet<CrmLead> CrmLeads => Set<CrmLead>();
    public DbSet<CrmActivity> CrmActivities => Set<CrmActivity>();

    // Ranger
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<RangerAgent> RangerAgents => Set<RangerAgent>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<TicketWatcher> TicketWatchers => Set<TicketWatcher>();

    // Sheriff
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<HeartbeatControl> HeartbeatControls => Set<HeartbeatControl>();
    public DbSet<HealthCheck> HealthChecks => Set<HealthCheck>();
    public DbSet<JobRun> JobRuns => Set<JobRun>();

    // Caboose
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<Msid> Msids => Set<Msid>();
    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();

    // Bounty
    public DbSet<RewardOffer> RewardOffers => Set<RewardOffer>();
    public DbSet<RewardRule> RewardRules => Set<RewardRule>();
    public DbSet<StarEntry> StarEntries => Set<StarEntry>();
    public DbSet<DrawingEntry> DrawingEntries => Set<DrawingEntry>();
    public DbSet<DrawingWinner> DrawingWinners => Set<DrawingWinner>();

    /// <summary>Who is making changes through this context (the signed-in employee), for history. Empty = the system.</summary>
    public string? ActorName { get; set; }
    public int? ActorId { get; set; }

    /// <summary>Turned off while loading sample data, so the logs start empty.</summary>
    public bool TrackHistory { get; set; } = true;

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // SQLite can't sum or sort decimals, so store money as REAL there. SQL Server keeps real decimals.
        if (Database.IsSqlite()) b.Properties<decimal>().HaveConversion<double>();
        else b.Properties<decimal>().HavePrecision(14, 8);
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<User>().HasIndex(u => u.Email).IsUnique();
        m.Entity<Customer>().HasIndex(c => c.Account).IsUnique();
        m.Entity<Customer>().HasIndex(c => c.Username).IsUnique();
        m.Entity<Customer>().HasIndex(c => c.Esiid);
        m.Entity<ShopProduct>().HasIndex(p => p.Sku).IsUnique();
        m.Entity<Bookmark>().HasIndex(b => new { b.UserId, b.CustomerId }).IsUnique();
        m.Entity<HistoryEntry>().HasIndex(h => new { h.CustomerId, h.Group });
        m.Entity<RangerAgent>().HasIndex(a => a.UserId).IsUnique();
        m.Entity<User>().HasOne(u => u.Manager).WithMany().HasForeignKey(u => u.ManagerId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<RangerAgent>().HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId);
        m.Entity<RangerAgent>().HasOne(a => a.AddedBy).WithMany().HasForeignKey(a => a.AddedById).OnDelete(DeleteBehavior.NoAction);
        m.Entity<SupportTicket>().HasOne(t => t.AssignedTo).WithMany().HasForeignKey(t => t.AssignedToId).OnDelete(DeleteBehavior.NoAction);
        m.Entity<SupportTicket>().HasOne(t => t.ManagerRequestedBy).WithMany().HasForeignKey(t => t.ManagerRequestedById).OnDelete(DeleteBehavior.NoAction);
        m.Entity<TicketWatcher>().HasIndex(w => new { w.TicketId, w.UserId }).IsUnique();
        m.Entity<ContactMessage>().HasOne(x => x.HandledBy).WithMany().HasForeignKey(x => x.HandledById).OnDelete(DeleteBehavior.NoAction);
        m.Entity<ContactMessage>().HasOne(x => x.RepliedBy).WithMany().HasForeignKey(x => x.RepliedById).OnDelete(DeleteBehavior.NoAction);
        m.Entity<ContactMessage>().HasOne(x => x.ResolvedBy).WithMany().HasForeignKey(x => x.ResolvedById).OnDelete(DeleteBehavior.NoAction);
        m.Entity<HeartbeatControl>().HasIndex(h => h.Key).IsUnique();
        m.Entity<Msid>().HasIndex(x => x.Code).IsUnique();
        m.Entity<PromoCode>().HasIndex(x => x.Code).IsUnique();
        m.Entity<StarEntry>().HasIndex(x => x.PaymentId);
        m.Entity<DrawingEntry>().HasIndex(x => new { x.Month, x.CustomerId }).IsUnique(); // one entry per person per month
        m.Entity<DrawingWinner>().HasIndex(x => x.Month).IsUnique();                     // one winner per month
    }

    // ---- History: every change to the tables in Tracked is written to History automatically ----------------

    /// <summary>Tracked tables: type → (model name as ebunny shows it, log group).</summary>
    public static readonly Dictionary<Type, (string Model, string Group)> Tracked = new()
    {
        [typeof(Customer)] = ("TicketCustomer_model", "Account Attributes"),
        [typeof(CustomerFlag)] = ("ItemFlag_model", "Account Attributes"),
        [typeof(PlanTerm)] = ("ItemPlanTerm_model", "Account Attributes"),
        [typeof(ServiceAddress)] = ("ItemServiceAddress_model", "Account Attributes"),
        [typeof(CustomerProduct)] = ("ItemProduct_model", "Products"),
        [typeof(Payment)] = ("ItemPayment_model", "Payments"),
        [typeof(PaymentMethod)] = ("ItemPaymentMethod_model", "Payments"),
        [typeof(Bill)] = ("ItemFileBill_model", "Billing & Files"),
        [typeof(CustomerFile)] = ("ItemFile_model", "Billing & Files"),
        [typeof(ErcotTransaction)] = ("ItemErcot_model", "EDI Transactions"),
        [typeof(ContactLog)] = ("ItemContactLog_model", "Emails"),
        [typeof(Note)] = ("ItemNote_model", "Notes"),
        [typeof(PhoneCall)] = ("ItemPhonecall_model", "Contact"),
        [typeof(QueueLog)] = ("TicketQueue_model", "Queues"),
        [typeof(WorkItem)] = ("TicketWorkItem_model", "Queues"),
        [typeof(Plan)] = ("Plan_model", "Admin Changes"),
        [typeof(Rate)] = ("Rate_model", "Admin Changes"),
        [typeof(Market)] = ("Market_model", "Admin Changes"),
        [typeof(User)] = ("User_model", "Admin Changes"),
        [typeof(Role)] = ("Role_model", "Admin Changes"),
    };

    private static readonly HashSet<string> Hidden = ["PasswordHash", "UpdatedAt"];

    public override int SaveChanges() => SaveChangesAsync().GetAwaiter().GetResult();

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var e in ChangeTracker.Entries<Customer>().Where(e => e.State == EntityState.Modified))
            e.Entity.UpdatedAt = DateTime.UtcNow;

        if (!TrackHistory) return await base.SaveChangesAsync(ct);

        // Collect history before saving (new rows get their IDs during the save, so fill those in after).
        var pending = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && Tracked.ContainsKey(e.Entity.GetType()))
            .Select(e => (Entry: e, History: Describe(e)))
            .Where(x => x.History != null).ToList();

        var saved = await base.SaveChangesAsync(ct);
        if (pending.Count == 0) return saved;

        foreach (var (entry, history) in pending)
        {
            if (history!.Action == "created")
            {
                history.RecordId = (int)entry.Property("Id").CurrentValue!;
                history.CustomerId = entry.Entity is Customer c ? c.Id : entry.Metadata.FindProperty("CustomerId") != null ? entry.Property("CustomerId").CurrentValue as int? : null;
            }
            History.Add(history);
        }
        await base.SaveChangesAsync(ct);
        return saved;
    }

    private HistoryEntry? Describe(EntityEntry e)
    {
        var (model, group) = Tracked[e.Entity.GetType()];
        var action = e.State switch { EntityState.Added => "created", EntityState.Deleted => "deleted", _ => "updated" };
        var changes = new Dictionary<string, object?>();
        foreach (var p in e.Properties.Where(p => !Hidden.Contains(p.Metadata.Name)))
        {
            if (e.State == EntityState.Modified)
            {
                if (p.IsModified && !Equals(p.OriginalValue, p.CurrentValue)) changes[p.Metadata.Name] = new[] { p.OriginalValue, p.CurrentValue };
            }
            else changes[p.Metadata.Name] = e.State == EntityState.Deleted ? p.OriginalValue : p.CurrentValue;
        }
        if (e.State == EntityState.Modified && changes.Count == 0) return null;

        var label = e.Metadata.ClrType.Name;
        var summary = action switch
        {
            "updated" => $"{label} changed: {string.Join(", ", changes.Keys)}",
            _ => $"{label} {action}",
        };
        // The contact log goes under Emails or (texts) Contact
        if (e.Entity is ContactLog log && log.Channel == "SMS") group = "Contact";
        return new HistoryEntry
        {
            RecordId = e.State == EntityState.Added ? 0 : (int)e.Property("Id").OriginalValue!,
            CustomerId = e.Entity is Customer cust ? cust.Id : e.Properties.FirstOrDefault(p => p.Metadata.Name == "CustomerId")?.CurrentValue as int?,
            UserId = ActorId, Who = ActorName ?? "System", Group = group, Model = model, RecordType = label, Action = action,
            Summary = summary.Length > 250 ? summary[..250] : summary, Changes = JsonSerializer.Serialize(changes),
        };
    }

    /// <summary>Adds a history line for something done to a record that isn't a change to it (emailed, regenerated…).</summary>
    public void Record(int? customerId, string group, string model, string recordType, int recordId, string action, string summary) =>
        History.Add(new HistoryEntry
        {
            CustomerId = customerId, UserId = ActorId, Who = ActorName ?? "System", Group = group, Model = model,
            RecordType = recordType, RecordId = recordId, Action = action, Summary = summary,
        });
}
