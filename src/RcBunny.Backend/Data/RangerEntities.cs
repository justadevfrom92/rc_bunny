namespace RcBunny.Data;

// ---- Ranger ---------------------------------------------------------------------------------------------------

/// <summary>A support ticket. Customers open them from My Account; staff can also make one from a web message.</summary>
public class SupportTicket
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Topic { get; set; } = "Billing";
    /// <summary>new, open, waiting (on the customer), solved or closed.</summary>
    public string Status { get; set; } = "new";
    /// <summary>normal or high.</summary>
    public string Priority { get; set; } = "normal";
    public int? AssignedToId { get; set; }
    public User? AssignedTo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FirstResponseAt { get; set; }
    public DateTime? SolvedAt { get; set; }
    /// <summary>The customer's rating once it's solved: good or bad.</summary>
    public string? Rating { get; set; }
    public string? RatingComment { get; set; }
}

public class TicketMessage
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string Author { get; set; } = "";
    public bool FromCustomer { get; set; }
    /// <summary>Internal notes are only seen by staff.</summary>
    public bool Internal { get; set; }
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Someone on the ticket team. Managers can assign tickets to anyone and take tickets over.</summary>
public class RangerAgent
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    /// <summary>"In-office" or "3rd party".</summary>
    public string Team { get; set; } = "In-office";
    public bool IsManager { get; set; }
    public bool Active { get; set; } = true;
}
