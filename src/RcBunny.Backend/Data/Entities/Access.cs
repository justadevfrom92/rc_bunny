namespace RcBunny.Data.Entities;

/// <summary>A role's Perms holds admin app keys (corral, sheriff, …) plus extra rights (refunds, delete).</summary>
public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<string> Perms { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<User> Users { get; set; } = [];

    public bool IsAdministrator => Name == "Administrator";
}

/// <summary>
/// An employee (or 3rd-party company person) who signs in to the admin.
/// Access = role perms + ExtraPerms − DeniedPerms, and only while Active and before AccessEndsOn.
/// </summary>
public class User
{
    public int Id { get; set; }
    public string? EmployeeId { get; set; }
    public int? RoleId { get; set; }
    public Role? Role { get; set; }
    public bool Active { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public string Name { get; set; } = "";
    public string? Title { get; set; }
    public string? Department { get; set; }
    /// <summary>3rd-party company the person works for (null = in-office employee).</summary>
    public string? Company { get; set; }
    /// <summary>Last day access works (contractors). Checked on every request.</summary>
    public DateOnly? AccessEndsOn { get; set; }
    public int? ManagerId { get; set; }
    public User? Manager { get; set; }
    public string? PhoneExt { get; set; }
    public string? Location { get; set; }
    public DateOnly? HiredOn { get; set; }
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    /// <summary>Apps or rights this person has beyond their role.</summary>
    public List<string> ExtraPerms { get; set; } = [];
    /// <summary>Apps or rights their role has that this person may not use.</summary>
    public List<string> DeniedPerms { get; set; } = [];
    public string? AccessNote { get; set; }
    /// <summary>
    /// Bumped whenever anything that affects this person's access changes (role, perms, company, end date).
    /// Sent on every API response (X-Access-Version) so the React client refreshes its copy of the person's access.
    /// </summary>
    public int AccessVersion { get; set; } = 1;
    /// <summary>Changes when the password changes or the person is disabled: every existing sign-in stops working.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Bookmark> Bookmarks { get; set; } = [];

    public bool IsExternal => !string.IsNullOrEmpty(Company);

    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => w[0]));
}

public class Bookmark
{
    public int UserId { get; set; }
    public User? User { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Who changed whose access, signed in, logged in as someone, etc. (Sheriff → Timeline).</summary>
public class AuditEntry
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Subject { get; set; }
    public string? Detail { get; set; }
    public string? Ip { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
