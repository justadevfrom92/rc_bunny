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
