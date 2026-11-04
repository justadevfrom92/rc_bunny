namespace RcBunny.Data;

// Every table in the app. The website project compiles this file too, so both apps share one model.

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int RoleId { get; set; }
    public Role? Role { get; set; }
    public bool Active { get; set; } = true;
    /// <summary>For people from a 3rd-party company (e.g. a support vendor); null for our own staff.</summary>
    public string? Company { get; set; }
}

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Comma-separated permission keys, e.g. "customers,refunds". See Permissions.All.</summary>
    public string Permissions { get; set; } = "";

    public List<string> PermissionList() => Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
}

public class Market
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Utility delivery charge, cents per kWh.</summary>
    public decimal DeliveryCentsPerKwh { get; set; }
    /// <summary>Utility delivery charge, dollars per month.</summary>
    public decimal DeliveryPerMonth { get; set; }
    public DateOnly? DeliveryEffectiveOn { get; set; }
    /// <summary>Short name (ONCOR), pricing region (ONCOR, CENTRAL…) and the utility's outage phone.</summary>
    public string Short { get; set; } = "";
    public string Region { get; set; } = "";
    public string Phone { get; set; } = "";
}

public class ZipRange
{
    public int Id { get; set; }
    public int From { get; set; }
    public int To { get; set; }
    public int MarketId { get; set; }
}

public class Plan
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>"Residential" or "Business".</summary>
    public string Type { get; set; } = "Residential";
    public int TermMonths { get; set; }
    public decimal MonthlyFee { get; set; }
    public int GreenPercent { get; set; }
    public bool Active { get; set; } = true;
    /// <summary>What the rate rolls to at the end of the term, and the early termination fee as shown ("-" for none).</summary>
    public string Rolloff { get; set; } = "None";
    public string Etf { get; set; } = "-";
    /// <summary>Comma-separated tags (month, fixed, free…) and perks (one per line) shown on the plan cards.</summary>
    public string Tags { get; set; } = "";
    public string Perks { get; set; } = "";
    public string Slug { get; set; } = "";
}

/// <summary>A named list of plans the website shows together (Homepage Featured, Residential Plans Page…).</summary>
public class PlanGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    /// <summary>Plan codes in order, comma-separated.</summary>
    public string PlanCodes { get; set; } = "";
}

/// <summary>Cents per kWh added to (or taken off) a rate for a contract length, per pricing region.</summary>
public class TermDiscount
{
    public int Id { get; set; }
    public int Term { get; set; }
    public string Region { get; set; } = "";
    public decimal Discount { get; set; }
}

/// <summary>The early termination fee for a contract length.</summary>
public class TermEtf
{
    public int Id { get; set; }
    public int Term { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>An add-on in Build Your Own Plan: a discount or extra on the rate, or a monthly charge.</summary>
public class ByopProduct
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Model { get; set; } = "";
    public string Type { get; set; } = "";
    public decimal RateAdj { get; set; }
    public decimal Monthly { get; set; }
    public string Step { get; set; } = "";
    public bool Active { get; set; } = true;
    public string? Description { get; set; }
    public int Position { get; set; }
}

public class Rate
{
    public int Id { get; set; }
    public int PlanId { get; set; }
    public int MarketId { get; set; }
    public decimal EnergyCents { get; set; }
    public DateOnly EffectiveOn { get; set; }
}

public class Customer
{
    public int Id { get; set; }
    public string Account { get; set; } = "";
    /// <summary>The internal ID (ebunny's "ticket").</summary>
    public string Ticket { get; set; } = "";
    public string Name { get; set; } = "";
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PhoneType { get; set; }
    public string Language { get; set; } = "English";
    public string? SsnLast4 { get; set; }
    public int? TecScore { get; set; }
    public string? Ip { get; set; }
    public string? IpLocation { get; set; }

    // Service address and meter
    public string Address { get; set; } = "";
    public string? Unit { get; set; }
    public string City { get; set; } = "";
    public string Zip { get; set; } = "";
    public int? MarketId { get; set; }
    public Market? Market { get; set; }
    /// <summary>The meter's ID with ERCOT (Texas grid). Empty until we know it.</summary>
    public string? Esiid { get; set; }
    public string? MeterNumber { get; set; }
    public string? LoadProfile { get; set; }
    public string? LoadZone { get; set; }
    public string? MeterType { get; set; }

    // Billing address (blank = same as service)
    public string? BillingStreet { get; set; }
    public string? BillingUnit { get; set; }
    public string? BillingCity { get; set; }
    public string? BillingState { get; set; }
    public string? BillingZip { get; set; }

    // Order and status
    /// <summary>"Residential" or "Small Business".</summary>
    public string Type { get; set; } = "Residential";
    /// <summary>One of Customers.Statuses (Submitted, Pending - …, Good - On Flow, …).</summary>
    public string Status { get; set; } = "Submitted";
    public string? StatusInfo { get; set; }
    /// <summary>An exception holding the order up (Customers.Exceptions), or empty.</summary>
    public string? Exception { get; set; }
    /// <summary>"switch" or "move".</summary>
    public string MoveSwitch { get; set; } = "switch";
    public DateOnly? RequestedStart { get; set; }
    public DateOnly? ActualStart { get; set; }
    /// <summary>Phone, Website, Referral or Power to Choose.</summary>
    public string Source { get; set; } = "Phone";
    public int? PlanId { get; set; }
    public Plan? Plan { get; set; }
    public string? RateClass { get; set; }

    // Money
    public decimal Balance { get; set; }
    public DateOnly? DueDate { get; set; }
    /// <summary>Deposit the customer still has to pay, and the deposit we're holding for them.</summary>
    public decimal DepositDue { get; set; }
    public decimal DepositHeld { get; set; }

    // Products that are also yes/no fields
    public bool Autopay { get; set; }
    public bool Paperless { get; set; }
    public bool PeakPerks { get; set; }

    // Marketing
    public string Channel { get; set; } = "1 - Organic (default)";
    /// <summary>The marketing source (MSID) and promo code they signed up with.</summary>
    public string? Msid { get; set; }
    public string? PromoCode { get; set; }
    public bool MarketingOptIn { get; set; } = true;

    /// <summary>Reward stars to spend (Bounty).</summary>
    public int Stars { get; set; }

    // My Account
    public string? Username { get; set; }
    public string? PasswordHash { get; set; }
    /// <summary>Other account numbers this login can switch to, comma-separated.</summary>
    public string? LinkedAccounts { get; set; }
    /// <summary>People who may call about the account: "name|phone|added" lines.</summary>
    public string? AuthorizedUsers { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The authorized users as (name, phone, added) entries.</summary>
    public List<(string Name, string Phone, string Added)> AuthorizedUserList() =>
        (AuthorizedUsers ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Split('|')).Select(p => (p[0], p.ElementAtOrDefault(1) ?? "", p.ElementAtOrDefault(2) ?? "")).ToList();
}

public class Bill
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Reference { get; set; } = "";
    public string BillType { get; set; } = "Regular";
    public DateOnly BilledOn { get; set; }
    public DateOnly DueOn { get; set; }
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public int Kwh { get; set; }
    public decimal Amount { get; set; }
    public decimal AmountPaid { get; set; }
    public DateOnly? PaidOn { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Invoice { get; set; }
    public bool Ontime { get; set; } = true;
}

public class PaymentMethod
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    /// <summary>"Card" or "Bank".</summary>
    public string Type { get; set; } = "Card";
    public string Last4 { get; set; } = "";
    public string? Expires { get; set; }
    public string? Nickname { get; set; }
    /// <summary>Who holds it: Stripe, Authorize.net…</summary>
    public string Vendor { get; set; } = "Stripe";
    public bool Autopay { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RemovedAt { get; set; }

    public string Label() => $"{Nickname ?? Type} ending {Last4}";
}

public class Payment
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Reference { get; set; } = "";
    public DateOnly PaidOn { get; set; }
    public TimeOnly? PaidTime { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "Card";
    public int? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    /// <summary>Success, Pending, Failed or Reversed.</summary>
    public string Status { get; set; } = "Success";
    /// <summary>"Balance Payment", "Deposit" or "Balance Adjustment".</summary>
    public string Kind { get; set; } = "Balance Payment";
    /// <summary>Where it came from: My Account, Phone, AutoPay, Finance, Corral…</summary>
    public string Source { get; set; } = "";
    public string? Confirmation { get; set; }
    public DateTime? ReversedAt { get; set; }
    public string? ReversedBy { get; set; }
}

public class Note
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Author { get; set; } = "";
    /// <summary>One of Customers.NoteCategories, with one of its actions.</summary>
    public string? Category { get; set; }
    public string? Action { get; set; }
    /// <summary>Low, Medium or High.</summary>
    public string? Priority { get; set; }
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? UserId { get; set; }
    /// <summary>Set when the note was written on a Ranger ticket (it shows on the ticket and the account).</summary>
    public int? SupportTicketId { get; set; }
}

public class ContactMessage
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string Topic { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>The account it's about, saved once someone replies.</summary>
    public int? CustomerId { get; set; }
    public DateTime? HandledAt { get; set; }
    public int? HandledById { get; set; }
    public User? HandledBy { get; set; }
    public string? Reply { get; set; }
    public DateTime? RepliedAt { get; set; }
    public int? RepliedById { get; set; }
    public User? RepliedBy { get; set; }
    public bool Handled => HandledAt != null;
}

/// <summary>
/// One line of history: something about an account (or an admin setting) was created, changed or deleted.
/// Written automatically by AppDbContext for the tables listed in History.Tracked. Shown as the account's logs
/// (Corral → account → Logs) and on Sheriff → staff → History.
/// </summary>
public class HistoryEntry
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public int? UserId { get; set; }
    public string Who { get; set; } = "";
    /// <summary>Which log it belongs to: Account Attributes, Products, Payments, Notes… (History.Groups).</summary>
    public string Group { get; set; } = "";
    /// <summary>The kind of record, as ebunny names it (ItemNote_model…).</summary>
    public string Model { get; set; } = "";
    public string RecordType { get; set; } = "";
    public int RecordId { get; set; }
    /// <summary>created, updated, deleted, or something done to it (emailed, regenerated…).</summary>
    public string Action { get; set; } = "";
    public string Summary { get; set; } = "";
    /// <summary>JSON: the changed fields as {field: [before, after]}, or every field for created/deleted.</summary>
    public string? Changes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

