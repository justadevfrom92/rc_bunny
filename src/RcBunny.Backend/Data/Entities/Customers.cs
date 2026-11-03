namespace RcBunny.Data.Entities;

/// <summary>A customer account. Also the My Account sign-in (Username or Email + Password).</summary>
public class Customer
{
    public int Id { get; set; }
    public string Account { get; set; } = "";
    public string Ticket { get; set; } = "";
    public string Name { get; set; } = "";
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string Type { get; set; } = "Residential";   // Residential | Small Business
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string Zip { get; set; } = "";
    public int? MarketId { get; set; }
    public Market? Market { get; set; }
    public string? Esiid { get; set; }
    public int? PlanId { get; set; }
    public Plan? Plan { get; set; }
    public string Status { get; set; } = "Submitted";
    public string? Exception { get; set; }
    public string Source { get; set; } = "Website";
    public string EnrollmentType { get; set; } = "Switch";
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal Balance { get; set; }
    public decimal DepositDue { get; set; }
    public bool Autopay { get; set; }
    public bool Paperless { get; set; }
    public bool PeakPerks { get; set; }
    public bool MarketingOptIn { get; set; } = true;
    public int Stars { get; set; }
    public string Language { get; set; } = "English";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? ReferralCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Payment> Payments { get; set; } = [];
    public List<Bill> Bills { get; set; } = [];
    public List<Note> Notes { get; set; } = [];
}

public class Payment
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string Reference { get; set; } = "";
    public DateOnly PaidOn { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "";
    public string Source { get; set; } = "";
    public string Status { get; set; } = "Success";   // Success | Failed | Reversed | Pending
    public string Kind { get; set; } = "Balance Payment";
    public string? Confirmation { get; set; }
    public DateTime? ReversedAt { get; set; }
    public int? ReversedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Bill
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string Reference { get; set; } = "";
    public DateOnly BilledOn { get; set; }
    public DateOnly? DueOn { get; set; }
    public int Kwh { get; set; }
    public decimal Amount { get; set; }
    public string? Invoice { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Note
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? UserId { get; set; }
    public string Author { get; set; } = "System";
    public string? Disposition { get; set; }
    public string? Category { get; set; }
    public string? Action { get; set; }
    public string? Priority { get; set; }
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Website Contact Us form submissions.</summary>
public class ContactMessage
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string Topic { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Ip { get; set; }
    public DateTime? HandledAt { get; set; }
    public int? HandledBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
