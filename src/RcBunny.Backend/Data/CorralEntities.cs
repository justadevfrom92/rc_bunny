namespace RcBunny.Data;

// ---- Corral: everything on an account besides the core customer, bill, payment and note tables ----------------

/// <summary>A flag on an account (VIP - Critical Care, SOLAR CUSTOMER…). Some show a banner on the account page.</summary>
public class CustomerFlag
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Flag { get; set; } = "";
    public string Author { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RemovedAt { get; set; }
}

/// <summary>A product on an account (AutoPay, Peak Perks, a protection plan…).</summary>
public class CustomerProduct
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string Product { get; set; } = "";
    /// <summary>Extra details as "key: value" pairs separated by " · " (installments, first due date…).</summary>
    public string? Details { get; set; }
    public string Author { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RemovedAt { get; set; }
}

/// <summary>A plan the account was on, is on, or will be on (Plan Change Log).</summary>
public class PlanTerm
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int? PlanId { get; set; }
    public Plan? Plan { get; set; }
    public string RateClass { get; set; } = "";
    /// <summary>Cents per kWh at 2,000 kWh.</summary>
    public decimal Rate2000 { get; set; }
    /// <summary>Dollars per kWh.</summary>
    public decimal EnergyCharge { get; set; }
    public DateTime OrderedAt { get; set; } = DateTime.UtcNow;
    public DateOnly? ContractStart { get; set; }
    public DateOnly? ContractEnd { get; set; }
    /// <summary>current, ended or not started.</summary>
    public string Status { get; set; } = "not started";
}

/// <summary>An address the account had service at (Address Change Log).</summary>
public class ServiceAddress
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string? Esiid { get; set; }
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string Zip { get; set; } = "";
    public DateTime OrderedAt { get; set; } = DateTime.UtcNow;
    public DateOnly? ServiceStart { get; set; }
    public DateOnly? ServiceEnd { get; set; }
}

/// <summary>When the account entered and left one of the order-flow queues (TECH → Move Queues).</summary>
public class QueueLog
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Queue { get; set; } = "";
    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExitedAt { get; set; }
    public string? Note { get; set; }
    public string? Who { get; set; }
}

/// <summary>A file on an account: an upload (Path set) or a document we generate (welcome, efl, tos, yrac, bill).</summary>
public class CustomerFile
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string Name { get; set; } = "";
    /// <summary>upload, welcome, efl, tos, yrac or bill.</summary>
    public string Kind { get; set; } = "upload";
    /// <summary>Where an uploaded file is stored (under Storage:Root). Empty for generated documents.</summary>
    public string? Path { get; set; }
    public int Regenerated { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>An email or text to or from a customer (Contact Log, Corral → SMS).</summary>
public class ContactLog
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    /// <summary>"Email" or "SMS".</summary>
    public string Channel { get; set; } = "Email";
    public string Template { get; set; } = "";
    public string? Body { get; set; }
    public string? Phone { get; set; }
    /// <summary>"out" (we sent it) or "in" (the customer sent it).</summary>
    public string Direction { get; set; } = "out";
    /// <summary>queued, sent, not sent, suppressed or logged (entered by hand).</summary>
    public string Status { get; set; } = "queued";
    /// <summary>Resolved, Incomplete, Follow-up Needed, Escalated or No Answer.</summary>
    public string? Resolution { get; set; }
    public string? ResolvedBy { get; set; }
    public string? Who { get; set; }
    /// <summary>The employee who sent it (null for the system).</summary>
    public int? UserId { get; set; }
    /// <summary>The Rodeo campaign it went out in.</summary>
    public int? CampaignId { get; set; }
    public DateTime? SentAt { get; set; }
    /// <summary>Email tracking: opened, a link clicked, or dropped (bounced).</summary>
    public DateTime? OpenedAt { get; set; }
    public DateTime? ClickedAt { get; set; }
    public DateTime? DroppedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PhoneCall
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string Phone { get; set; } = "";
    /// <summary>"inbound" or "outbound".</summary>
    public string Direction { get; set; } = "inbound";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public int DurationSec { get; set; }
    /// <summary>What the call was about.</summary>
    public string? Issue { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    /// <summary>The phone system's agent ID.</summary>
    public string? AgentId { get; set; }
    public string? Transcript { get; set; }
    public string? Resolution { get; set; }
    public string? ResolvedBy { get; set; }
}

/// <summary>A call to an outside service (Stripe, Experian…), or the record that it wasn't made because it isn't set up.</summary>
public class ApiLog
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public string Api { get; set; } = "";
    public string Action { get; set; } = "";
    /// <summary>ok, failed, skipped (not set up) or not built.</summary>
    public string Status { get; set; } = "";
    public string? Request { get; set; }
    public string? Response { get; set; }
    public int? Milliseconds { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>An employee's bookmarked account (Customer Search → Bookmarks).</summary>
public class Bookmark
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
}

/// <summary>An account that needs someone to look at it, in one of the work queues (Corral → Exceptions).</summary>
public class WorkItem
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    /// <summary>A key of Customers.WorkQueues (unprocessed-orders, ercot-exceptions…).</summary>
    public string Queue { get; set; } = "";
    public string Summary { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
}

/// <summary>A request to or answer from ERCOT about a meter (814 enrollments, 650 service orders, 867 usage…).</summary>
public class ErcotTransaction
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string Esiid { get; set; } = "";
    public DateOnly TransDate { get; set; }
    /// <summary>"request" (we sent it) or "response" (ERCOT sent it).</summary>
    public string Purpose { get; set; } = "request";
    public DateOnly? ScheduledOn { get; set; }
    /// <summary>The EDI transaction number, e.g. 814_01.</summary>
    public string TransType { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Tracking { get; set; }
    /// <summary>linked, unlinked or cancelled.</summary>
    public string Status { get; set; } = "linked";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ---- Corral → Products (the website Shop) ---------------------------------------------------------------------

public class ShopProduct
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>A key of Shop.Categories.</summary>
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    /// <summary>The price before a sale (shows as "was").</summary>
    public decimal? Was { get; set; }
    public decimal Rating { get; set; }
    public int Reviews { get; set; }
    /// <summary>Reward stars earned by buying it.</summary>
    public int Stars { get; set; }
    public bool InStock { get; set; } = true;
    public string? Badge { get; set; }
    public string Icon { get; set; } = "plug";
    public string? Blurb { get; set; }
    public bool Active { get; set; } = true;
    public int Position { get; set; }
}

public class ShopOrder
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    /// <summary>JSON list of {sku, name, qty, price, line, monthly, stars}.</summary>
    public string Items { get; set; } = "[]";
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Shipping { get; set; }
    public decimal Total { get; set; }
    /// <summary>Protection plans added to the monthly bill.</summary>
    public decimal Monthly { get; set; }
    public int Stars { get; set; }
    /// <summary>"card" or "bill".</summary>
    public string PayWith { get; set; } = "card";
    public int? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    /// <summary>"name|street|city|state|zip".</summary>
    public string ShipTo { get; set; } = "";
    public string PaymentStatus { get; set; } = "Pending";
    /// <summary>Placed, Shipped, Delivered or Canceled.</summary>
    public string Status { get; set; } = "Placed";
    /// <summary>The employee who ordered by phone (empty = the customer, in My Account).</summary>
    public string? PlacedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ---- Corral → CRM -------------------------------------------------------------------------------------------

/// <summary>A broker or referral partner who sends us leads and earns commission on the ones we win.</summary>
public class CrmPartner
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Broker, Referral Partner, Property Manager, Home Builder or Realtor.</summary>
    public string Kind { get; set; } = "Broker";
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    /// <summary>"kwh" (rate × yearly kWh) or "flat" (rate × yearly value).</summary>
    public string CommissionBasis { get; set; } = "kwh";
    public decimal CommissionRate { get; set; } = 0.004m;
    /// <summary>Active, Paused or Ended.</summary>
    public string Status { get; set; } = "Active";
    public int? OwnerId { get; set; }
    public User? Owner { get; set; }
    public DateOnly? AgreementEndsOn { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public decimal CommissionFor(CrmLead lead) =>
        Math.Round(CommissionBasis == "kwh" ? lead.AnnualKwh * CommissionRate : lead.EstValue * CommissionRate, 2);
}

/// <summary>A person or business that might sign up, moving through the pipeline.</summary>
public class CrmLead
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Company { get; set; }
    public string? Title { get; set; }
    /// <summary>Residential, Small Business, Commercial or Multi-site.</summary>
    public string Kind { get; set; } = "Small Business";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public int? MarketId { get; set; }
    public Market? Market { get; set; }
    /// <summary>Website, Referral, Broker, Partner, Event, Outbound or Renewal.</summary>
    public string Source { get; set; } = "Website";
    public int? PartnerId { get; set; }
    public CrmPartner? Partner { get; set; }
    public int AnnualKwh { get; set; }
    /// <summary>Estimated yearly value in dollars.</summary>
    public decimal EstValue { get; set; }
    public int Sites { get; set; } = 1;
    public string? CurrentProvider { get; set; }
    public DateOnly? ContractEndsOn { get; set; }
    /// <summary>new, contacted, qualified, proposal, won or lost.</summary>
    public string Stage { get; set; } = "new";
    public DateTime StageChangedAt { get; set; } = DateTime.UtcNow;
    public string? LostReason { get; set; }
    public int? OwnerId { get; set; }
    public User? Owner { get; set; }
    /// <summary>The account it became, once won.</summary>
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public DateOnly? NextFollowUpOn { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public bool IsOpen => Stage is not ("won" or "lost");
}

/// <summary>A call, email, meeting, note, stage change or follow-up (a "task" with a due date) on a lead or partner.</summary>
public class CrmActivity
{
    public int Id { get; set; }
    public int? LeadId { get; set; }
    public CrmLead? Lead { get; set; }
    public int? PartnerId { get; set; }
    public CrmPartner? Partner { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    /// <summary>call, email, meeting, note, task or stage.</summary>
    public string Type { get; set; } = "note";
    public string Subject { get; set; } = "";
    public string? Body { get; set; }
    public DateOnly? DueOn { get; set; }
    public DateTime? DoneAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
