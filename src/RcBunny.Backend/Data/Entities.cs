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
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string Zip { get; set; } = "";
    public int? MarketId { get; set; }
    public Market? Market { get; set; }
    public int? PlanId { get; set; }
    public Plan? Plan { get; set; }
    /// <summary>"Residential" or "Small Business".</summary>
    public string Type { get; set; } = "Residential";
    /// <summary>The meter's ID with ERCOT (Texas grid). Empty until we know it.</summary>
    public string? Esiid { get; set; }
    public string Status { get; set; } = "Active";
    public decimal Balance { get; set; }
    public DateOnly? DueDate { get; set; }
    public bool Autopay { get; set; }
    /// <summary>My Account sign-in (optional).</summary>
    public string? Username { get; set; }
    public string? PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Bill
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public DateOnly BilledOn { get; set; }
    public DateOnly DueOn { get; set; }
    public int Kwh { get; set; }
    public decimal Amount { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public DateOnly PaidOn { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "Card";
    /// <summary>"Paid" or "Reversed".</summary>
    public string Status { get; set; } = "Paid";
    /// <summary>Where it came from: "My Account", "Phone", …</summary>
    public string Source { get; set; } = "";
}

public class Note
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Author { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ContactMessage
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string Topic { get; set; } = "";
    public string Message { get; set; } = "";
    public bool Handled { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ---- Corral ---------------------------------------------------------------------------------------------------

public class TextMessage
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public string Phone { get; set; } = "";
    public string Body { get; set; } = "";
    /// <summary>"out" (we sent it) or "in" (the customer sent it).</summary>
    public string Direction { get; set; } = "out";
    public string Author { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PhoneCall
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public string Phone { get; set; } = "";
    /// <summary>"in" or "out".</summary>
    public string Direction { get; set; } = "in";
    public int Minutes { get; set; }
    /// <summary>What happened, e.g. "Resolved", "Callback needed".</summary>
    public string Outcome { get; set; } = "";
    public string? Notes { get; set; }
    public string Author { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Partner
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>"Broker" or "Partner".</summary>
    public string Kind { get; set; } = "Broker";
    public string? Contact { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public decimal CommissionPercent { get; set; }
    public bool Active { get; set; } = true;
}

public class Lead
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Company { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    /// <summary>Residential, Small Business, Commercial or Multi-site.</summary>
    public string Kind { get; set; } = "Residential";
    /// <summary>Website, Referral, Broker, Partner, Event, Outbound or Renewal.</summary>
    public string Source { get; set; } = "Website";
    /// <summary>new, contacted, qualified, proposal, won or lost.</summary>
    public string Stage { get; set; } = "new";
    public int MonthlyKwh { get; set; }
    public int? PartnerId { get; set; }
    public Partner? Partner { get; set; }
    public int? OwnerId { get; set; }
    public User? Owner { get; set; }
    public string? LostReason { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
}

public class FollowUp
{
    public int Id { get; set; }
    public int LeadId { get; set; }
    public Lead? Lead { get; set; }
    public DateOnly DueOn { get; set; }
    public string Task { get; set; } = "";
    public bool Done { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public bool Active { get; set; } = true;
}

public class ProductOrder
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal Total { get; set; }
    /// <summary>New, Shipped, Delivered or Cancelled.</summary>
    public string Status { get; set; } = "New";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A request sent to ERCOT for a meter (start service, move in, stop service).</summary>
public class ErcotTransaction
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string Esiid { get; set; } = "";
    /// <summary>Switch, Move-In, Move-Out or Drop.</summary>
    public string Type { get; set; } = "Switch";
    /// <summary>Sent, Accepted, Rejected or Completed.</summary>
    public string Status { get; set; } = "Sent";
    public string? Message { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

/// <summary>An account that needs someone to look at it, in an exception queue.</summary>
public class WorkItem
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string Queue { get; set; } = "";
    public string Summary { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
}
