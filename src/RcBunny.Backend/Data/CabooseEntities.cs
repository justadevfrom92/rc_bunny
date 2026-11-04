namespace RcBunny.Data;

// ---- Caboose --------------------------------------------------------------------------------------------------

/// <summary>A credit (lowers the balance) or debit (raises it) someone asked for. It changes the balance only once approved.</summary>
public class LedgerEntry
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    /// <summary>"credit" or "debit".</summary>
    public string Kind { get; set; } = "credit";
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    /// <summary>pending, applied, rejected or deleted.</summary>
    public string Status { get; set; } = "pending";
    public string RequestedBy { get; set; } = "";
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Money paid back to a customer: a credit balance or a deposit. requested → approved → paid (or rejected).</summary>
public class Refund
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    /// <summary>"balance" (a credit balance) or "deposit".</summary>
    public string Kind { get; set; } = "balance";
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
    /// <summary>requested, approved, rejected or paid.</summary>
    public string Status { get; set; } = "requested";
    public string RequestedBy { get; set; } = "";
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }
    public string? CheckNumber { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A marketing source ID: where a customer came from (a mailer, a partner, a web campaign…).</summary>
public class Msid
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Channel { get; set; } = "Web";
    public bool Active { get; set; } = true;
}

public class PromoCode
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Bill credit the code gives, in dollars.</summary>
    public decimal Credit { get; set; }
    public DateOnly? StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public bool Active { get; set; } = true;
}
