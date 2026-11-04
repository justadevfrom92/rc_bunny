namespace RcBunny.Data;

// ---- Bounty ---------------------------------------------------------------------------------------------------

/// <summary>Something customers can spend stars on.</summary>
public class RewardOffer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Stars { get; set; }
    /// <summary>credit (bill credit), gift (e-gift card, sent by staff), drawing (monthly drawing entry) or product (sent by staff).</summary>
    public string Effect { get; set; } = "credit";
    /// <summary>For a credit, the dollars; for a gift or product, what to send.</summary>
    public string Value { get; set; } = "";
    public bool Active { get; set; } = true;
    public int Position { get; set; }
}

/// <summary>How many stars something earns, e.g. paying a bill.</summary>
public class RewardRule
{
    public int Id { get; set; }
    /// <summary>bill_paid (the nightly job awards it), or manual (staff award it).</summary>
    public string Event { get; set; } = "manual";
    public string Name { get; set; } = "";
    public int Stars { get; set; }
    public bool Active { get; set; } = true;
}

/// <summary>Stars earned (+) or spent (−). A redemption that staff have to send waits as "pending" until it's sent.</summary>
public class StarEntry
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int Stars { get; set; }
    public string Reason { get; set; } = "";
    public int? OfferId { get; set; }
    public RewardOffer? Offer { get; set; }
    /// <summary>null, pending or sent.</summary>
    public string? Fulfillment { get; set; }
    /// <summary>The payment that earned it (so the nightly job never awards a payment twice).</summary>
    public int? PaymentId { get; set; }
    public string By { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class DrawingEntry
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    /// <summary>yyyy-MM</summary>
    public string Month { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class DrawingWinner
{
    public int Id { get; set; }
    public string Month { get; set; } = "";
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string Prize { get; set; } = "";
    public string DrawnBy { get; set; } = "";
    public DateTime DrawnAt { get; set; } = DateTime.UtcNow;
}
