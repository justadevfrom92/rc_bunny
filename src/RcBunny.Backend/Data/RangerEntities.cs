namespace RcBunny.Data;

// ---- Ranger ---------------------------------------------------------------------------------------------------

/// <summary>A support ticket. Customers open them from My Account; they can also come in by email or phone.</summary>
public class SupportTicket
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Subject { get; set; } = "";
    /// <summary>One of Ranger.Topics: billing, service, plan, rewards, account, outage or other.</summary>
    public string Topic { get; set; } = "other";
    /// <summary>Where it came in: My Account, Email or Phone.</summary>
    public string Channel { get; set; } = "My Account";
    /// <summary>new, open, waiting (on the customer), solved or closed.</summary>
    public string Status { get; set; } = "new";
    /// <summary>low, normal, high or urgent.</summary>
    public string Priority { get; set; } = "normal";
    public int? AssignedToId { get; set; }
    public User? AssignedTo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>The last time anything happened on the ticket (a message, a note, a change).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FirstResponseAt { get; set; }
    public DateTime? SolvedAt { get; set; }
    /// <summary>The customer's rating once it's solved: good or bad.</summary>
    public string? Rating { get; set; }
    public string? RatingComment { get; set; }
    public DateTime? RatedAt { get; set; }
    /// <summary>An agent asked a manager to help (shown on the dashboard until a manager joins or takes over).</summary>
    public DateTime? ManagerRequestedAt { get; set; }
    public int? ManagerRequestedById { get; set; }
    public User? ManagerRequestedBy { get; set; }
    public string? ManagerRequestNote { get; set; }

    public string Number => $"RT-{10000 + Id}";
    public bool IsDone => Status is "solved" or "closed";
}

/// <summary>
/// One entry in a ticket's activity. Kind is customer (they wrote), agent (we replied), system (a change, like
/// "Casey took this ticket") or note (staff only, for tickets with no account to file the note on).
/// </summary>
public class TicketMessage
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string Kind { get; set; } = "customer";
    /// <summary>The person's name as shown (the customer's, or the employee's).</summary>
    public string Author { get; set; } = "";
    public int? UserId { get; set; }
    /// <summary>portal (My Account), email, or phone (a logged call: the customer doesn't see these).</summary>
    public string Via { get; set; } = "portal";
    /// <summary>For calls: in (they called us) or out (we called them).</summary>
    public string? Direction { get; set; }
    public int? DurationSec { get; set; }
    /// <summary>The email address or phone number used.</summary>
    public string? Contact { get; set; }
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>What the customer sees in My Account: their messages and our replies, not notes, calls or changes.</summary>
    public bool CustomerSees => Kind is "customer" || (Kind == "agent" && Via != "phone");
}

/// <summary>A file sent with a ticket message.</summary>
public class TicketAttachment
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int? MessageId { get; set; }
    public int? UserId { get; set; }
    public bool FromCustomer { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Under the storage folder.</summary>
    public string Path { get; set; } = "";
    public string Mime { get; set; } = "application/octet-stream";
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsImage => Mime is "image/png" or "image/jpeg" or "image/gif" or "image/webp";
    public bool IsPdf => Mime == "application/pdf";
    public bool IsText => Mime is "text/plain" or "text/csv";
}

/// <summary>Someone who joined a ticket (a manager helping, or the agent who had it before a take-over).</summary>
public class TicketWatcher
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Someone on the ticket team. Being on the team gives them Ranger.</summary>
public class RangerAgent
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    /// <summary>office (In-Office) or third_party (3rd Party).</summary>
    public string Team { get; set; } = "office";
    /// <summary>agent, manager, or company_admin (runs a 3rd-party company's people).</summary>
    public string Level { get; set; } = "agent";
    public bool Active { get; set; } = true;
    public int? AddedById { get; set; }
    public User? AddedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Ranger's lists.</summary>
public static class Ranger
{
    public static readonly Dictionary<string, (string Label, string Tone)> Statuses = new()
    {
        ["new"] = ("New", "info"), ["open"] = ("Open", "warn"), ["waiting"] = ("Waiting on Customer", ""), ["solved"] = ("Solved", "ok"), ["closed"] = ("Closed", ""),
    };

    public static readonly Dictionary<string, (string Label, string Tone)> Priorities = new()
    {
        ["low"] = ("Low", ""), ["normal"] = ("Normal", "info"), ["high"] = ("High", "warn"), ["urgent"] = ("Urgent", "bad"),
    };

    public static readonly Dictionary<string, string> Channels = new() { ["My Account"] = "💬", ["Email"] = "✉", ["Phone"] = "📞" };

    public static readonly Dictionary<string, string> Topics = new()
    {
        ["billing"] = "Billing & Payments", ["service"] = "Start, Stop or Move Service", ["plan"] = "My Plan", ["rewards"] = "Rangler Rewards",
        ["account"] = "My Account & Login", ["outage"] = "Power Outage", ["other"] = "Something Else",
    };

    public static readonly Dictionary<string, string> Via = new() { ["portal"] = "My Account", ["email"] = "Email", ["phone"] = "Phone call" };

    public static readonly Dictionary<string, string> Teams = new() { ["office"] = "In-Office", ["third_party"] = "3rd Party" };

    public static readonly Dictionary<string, string> Levels = new() { ["agent"] = "Agent", ["manager"] = "Manager", ["company_admin"] = "Company Admin" };

    public static readonly string[] Active = ["new", "open", "waiting"];

    public static readonly string[] AttachmentTypes = [".pdf", ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".txt", ".csv", ".doc", ".docx", ".xls", ".xlsx"];

    public static string MimeOf(string fileName) => System.IO.Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".webp" => "image/webp",
        ".txt" => "text/plain", ".csv" => "text/csv", _ => "application/octet-stream",
    };

    /// <summary>Add a message and keep the ticket's times and status in step.</summary>
    public static TicketMessage Post(AppDbContext db, SupportTicket t, string kind, string author, string body, int? userId = null)
    {
        var m = new TicketMessage { TicketId = t.Id, Kind = kind, Author = author, Body = body, UserId = userId };
        db.TicketMessages.Add(m);
        t.UpdatedAt = DateTime.UtcNow;
        if (kind == "agent")
        {
            t.FirstResponseAt ??= DateTime.UtcNow;
            if (t.Status == "new") t.Status = "open";
        }
        if (kind == "customer" && t.Status is "waiting" or "solved") t.Status = "open"; // the customer wrote back: it's ours again
        return m;
    }

    /// <summary>Save an uploaded file for a ticket message.</summary>
    public static async Task<TicketAttachment> SaveFileAsync(string storageRoot, TicketMessage m, string fileName, Stream content, int? userId, bool fromCustomer)
    {
        var safe = string.Concat(System.IO.Path.GetFileName(fileName).Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' ? ch : '-'));
        var relative = System.IO.Path.Combine("ranger", m.TicketId.ToString(), $"{Guid.NewGuid():N}"[..8] + "-" + safe);
        var full = System.IO.Path.Combine(storageRoot, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        await using (var file = File.Create(full)) await content.CopyToAsync(file);
        return new TicketAttachment
        {
            TicketId = m.TicketId, MessageId = m.Id, UserId = userId, FromCustomer = fromCustomer, Name = System.IO.Path.GetFileName(fileName),
            Path = relative, Mime = MimeOf(fileName), Size = new FileInfo(full).Length, CreatedAt = m.CreatedAt,
        };
    }
}
